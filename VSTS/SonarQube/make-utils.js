// modules
var AdmZip = require('adm-zip')
var check = require('validator')
var fs = require('fs')
var minimatch = require('minimatch')
var ncp = require('child_process')
var os = require('os');
var path = require('path')
var process = require('process')
var semver = require('semver');
var shell = require('shelljs')
var syncRequest = require('sync-request')

// global paths
var downloadPath = path.join(__dirname, '_download')

var makeOptions = require('./make-options.json')

// ------------------------------------------------------------------------------
// shell functions
// ------------------------------------------------------------------------------
var shellAssert = function () {
  var errMsg = shell.error()
  if (errMsg) {
    throw new Error(errMsg)
  }
}

var cd = function (dir) {
  shell.cd(dir)
  shellAssert()
}
exports.cd = cd

var cp = function (options, source, dest) {
  if (dest) {
    shell.cp(options, source, dest)
  } else {
    shell.cp(options, source)
  }

  shellAssert()
}
exports.cp = cp

var mkdir = function (options, target) {
  if (target) {
    shell.mkdir(options, target)
  } else {
    shell.mkdir(options)
  }

  shellAssert()
}
exports.mkdir = mkdir

var rm = function (options, target) {
  if (target) {
    shell.rm(options, target)
  } else {
    shell.rm(options)
  }

  shellAssert()
}
exports.rm = rm

var test = function (options, p) {
  var result = shell.test(options, p)
  shellAssert()
  return result
}
exports.test = test
// ------------------------------------------------------------------------------

// ------------------------------------------------------------------------------
// build functions
// ------------------------------------------------------------------------------
var addPath = function (directory) {
    var separator;
    if (os.platform() == 'win32') {
        separator = ';';
    }
    else {
        separator = ':';
    }

    var existing = process.env['PATH'];
    if (existing) {
        process.env['PATH'] = directory + separator + existing;
    }
    else {
        process.env['PATH'] = directory;
    }
}
exports.addPath = addPath;

var assert = function (value, name) {
  if (!value) {
    throw new Error('"' + name + '" cannot be null or empty.')
  }
}
exports.assert = assert

var banner = function (message, noBracket) {
  console.log()
  if (!noBracket) {
    console.log('------------------------------------------------------------')
  }
  console.log(message)
  if (!noBracket) {
    console.log('------------------------------------------------------------')
  }
  console.log()
}
exports.banner = banner

var buildNodeTask = function (taskPath, outDir) {
  var originalDir = pwd()
  cd(taskPath)
  if (test('-f', rp('package.json'))) {
    run('npm install')
  }
  run('tsc --outDir ' + outDir + ' --rootDir ' + taskPath)
  cd(originalDir)
}
exports.buildNodeTask = buildNodeTask

var copyGroup = function (group, sourceRoot, destRoot) {
  // example structure to copy a single file:
  // {
  //   "source": "foo.dll"
  // }
  //
  // example structure to copy an array of files/folders to a relative directory:
  // {
  //   "source": [
  //     "foo.dll",
  //     "bar",
  //   ],
  //   "dest": "baz/",
  //   "options": "-R"
  // }
  //
  // example to multiply the copy by .NET culture names supported by TFS:
  // {
  //   "source": "<CULTURE_NAME>/foo.dll",
  //   "dest": "<CULTURE_NAME>/"
  // }
  //

  // validate parameters
  assert(group, 'group')
  assert(group.source, 'group.source')
  if (typeof group.source === 'object') {
    assert(group.source.length, 'group.source.length')
    group.source.forEach(function (s) {
      assert(s, 'group.source[i]')
    })
  }

  assert(sourceRoot, 'sourceRoot')
  assert(destRoot, 'destRoot')

  // build the source array
  var source = typeof group.source === 'string' ? [group.source] : group.source
  source = source.map(function (val) { // root the paths
    return path.join(sourceRoot, val)
  })

  // create the destination directory
  var dest = group.dest ? path.join(destRoot, group.dest) : destRoot + '/'
  dest = path.normalize(dest)
  mkdir('-p', dest)

  // copy the files
  if (group.hasOwnProperty('options') && group.options) {
    cp(group.options, source, dest)
  } else {
    cp(source, dest)
  }
}

var copyGroups = function (groups, sourceRoot, destRoot) {
  assert(groups, 'groups')
  assert(groups.length, 'groups.length')
  groups.forEach(function (group) {
    copyGroup(group, sourceRoot, destRoot)
  })
}
exports.copyGroups = copyGroups

var copyTaskResources = function (taskMake, srcPath, destPath) {
  assert(taskMake, 'taskMake')
  assert(srcPath, 'srcPath')
  assert(destPath, 'destPath')

  // copy the globally defined set of default task resources
  makeOptions['taskResources'].forEach(function (item) {
    matchCopy(item, srcPath, destPath, { noRecurse: true })
  })

  // copy the locally defined set of resources
  if (taskMake.hasOwnProperty('cp')) {
    copyGroups(taskMake.cp, srcPath, destPath)
  }

  // remove the locally defined set of resources
  if (taskMake.hasOwnProperty('rm')) {
    removeGroups(taskMake.rm, destPath)
  }
}
exports.copyTaskResources = copyTaskResources

var downloadArchive = function (url, omitExtensionCheck) {
  // validate parameters
  if (!url) {
    throw new Error('Parameter "url" must be set.')
  }

  if (!omitExtensionCheck && !url.match(/\.zip$/)) {
    throw new Error('Expected .zip')
  }

  // skip if already downloaded and extracted
  var scrubbedUrl = url.replace(/[/:?]/g, '_')
  var targetPath = path.join(downloadPath, 'archive', scrubbedUrl)
  var marker = targetPath + '.completed'
  if (!test('-f', marker)) {
    // download the archive
    var archivePath = downloadFile(url)
    console.log('Extracting archive: ' + url)

    // delete any previously attempted extraction directory
    if (test('-d', targetPath)) {
      rm('-rf', targetPath)
    }

    // extract
    mkdir('-p', targetPath)
    var zip = new AdmZip(archivePath)
    zip.extractAllTo(targetPath)

    // write the completed marker
    fs.writeFileSync(marker, '')
  }

  return targetPath
}
exports.downloadArchive = downloadArchive

var downloadFile = function (url) {
  // validate parameters
  if (!url) {
    throw new Error('Parameter "url" must be set.')
  }

  // skip if already downloaded
  var scrubbedUrl = url.replace(/[/:?]/g, '_')
  var targetPath = path.join(downloadPath, 'file', scrubbedUrl)
  var marker = targetPath + '.completed'
  if (!test('-f', marker)) {
    console.log('Downloading file: ' + url)

    // delete any previous partial attempt
    if (test('-f', targetPath)) {
      rm('-f', targetPath)
    }

    // download the file
    mkdir('-p', path.join(downloadPath, 'file'))
    var result = syncRequest('GET', url)
    fs.writeFileSync(targetPath, result.getBody())

    // write the completed marker
    fs.writeFileSync(marker, '')
  }

  return targetPath
}
exports.downloadFile = downloadFile

var ensureExists = function (checkPath) {
  assert(checkPath, 'checkPath')
  var exists = test('-d', checkPath) || test('-f', checkPath)

  if (!exists) {
    fail(checkPath + ' does not exist')
  }
}
exports.ensureExists = ensureExists

var ensureTool = function (name, versionArgs, validate) {
  console.log(name + ' tool:')
  var toolPath = which(name)
  if (!toolPath) {
    fail(name + ' not found.  might need to run npm install')
  }

  if (versionArgs) {
    var result = exec(name + ' ' + versionArgs)
    if (typeof validate === 'string') {
      if (result.stdout.trim() !== validate) {
        fail('expected version: ' + validate)
      }
    } else {
      validate(result.stdout.trim())
    }
  }

  console.log(toolPath + '')
}
exports.ensureTool = ensureTool

var fail = function (message) {
  console.error('ERROR: ' + message)
  process.exit(1)
}
exports.fail = fail

var getExternals = function (externals, destRoot) {
  assert(externals, 'externals')
  assert(destRoot, 'destRoot')

  // .zip files
  if (externals.hasOwnProperty('archivePackages')) {
    externals.archivePackages.forEach(function (archive) {
      assert(archive.url, 'archive.url')
      assert(archive.dest, 'archive.dest')
      assert(archive.include, 'archive.include')

      // download and extract the archive package
      var archiveSource = downloadArchive(archive.url)

      // copy the files
      var archiveDest = path.join(destRoot, archive.dest)
      mkdir('-p', archiveDest)
      cp('-R', path.join(archiveSource, archive.include), archiveDest)
    })
  }
}
exports.getExternals = getExternals

var matchCopy = function (pattern, sourceRoot, destRoot, options) {
  assert(pattern, 'pattern')
  assert(sourceRoot, 'sourceRoot')
  assert(destRoot, 'destRoot')

  console.log(`copying ${pattern}`)

  // normalize first, so we can substring later
  sourceRoot = path.resolve(sourceRoot)
  destRoot = path.resolve(destRoot)

  matchFind(pattern, sourceRoot, options).forEach(function (item) {
    // create the dest dir based on the relative item path
    var relative = item.substring(sourceRoot.length + 1)
    assert(relative, 'relative') // should always be filterd out by matchFind
    var dest = path.dirname(path.join(destRoot, relative))
    mkdir('-p', dest)

    cp('-Rf', item, dest + '/')
  })
}
exports.matchCopy = matchCopy

var matchFind = function (pattern, root, options) {
  assert(pattern, 'pattern')
  assert(root, 'root')

  // determine whether to recurse
  options = options || {}
  var noRecurse = options.hasOwnProperty('noRecurse') && options.noRecurse
  delete options.noRecurse

  // merge specified options with defaults
  mergedOptions = { matchBase: true }
  Object.keys(options || {}).forEach(function (key) {
    mergedOptions[key] = options[key]
  })

  // normalize first, so we can substring later
  root = path.resolve(root)

  // determine the list of items
  var items
  if (noRecurse) {
    items = fs.readdirSync(root).map(function (name) {
      return path.join(root, name)
    })
  } else {
    items = find(root).filter(function (item) { // filter out the root folder
      return path.normalize(item) != root
    })
  }

  return minimatch.match(items, pattern, mergedOptions)
}
exports.matchFind = matchFind

var removeGroup = function (group, pathRoot) {
  // example structure to remove an array of files/folders:
  // {
  //   "items": [
  //     "foo.dll",
  //     "bar",
  //   ],
  //   "options": "-R"
  // }

  // validate parameters
  assert(group, 'group')
  assert(group.items, 'group.items')
  if (typeof group.items !== 'object') {
    throw new Error('Expected group.items to be an array')
  } else {
    assert(group.items.length, 'group.items.length')
    group.items.forEach(function (p) {
      assert(p, 'group.items[i]')
    })
  }

  assert(group.options, 'group.options')
  assert(pathRoot, 'pathRoot')

  // build the rooted items array
  var rootedItems = group.items.map(function (val) { // root the paths
    return path.join(pathRoot, val)
  })

  // remove the items
  rm(group.options, rootedItems)
}

var removeGroups = function (groups, pathRoot) {
  assert(groups, 'groups')
  assert(groups.length, 'groups.length')
  groups.forEach(function (group) {
    removeGroup(group, pathRoot)
  })
}
exports.removeGroups = removeGroups

var rp = function (relPath) {
  return path.join(pwd() + '', relPath)
}
exports.rp = rp

var run = function (cl, inheritStreams, noHeader) {
  if (!noHeader) {
    console.log()
    console.log('> ' + cl)
  }

  var options = {
    stdio: inheritStreams ? 'inherit' : 'pipe'
  }
  var output
  try {
    output = ncp.execSync(cl, options)
  } catch (err) {
    if (!inheritStreams) {
      console.error(err.output ? err.output.toString() : err.message)
    }

    process.exit(1)
  }

  return (output || '').toString().trim()
}
exports.run = run

var validateTask = function (task) {
  if (!task.id || !check.isUUID(task.id)) {
    fail('id is a required guid')
  }

  if (!task.name || !check.isAlphanumeric(task.name)) {
    fail('name is a required alphanumeric string')
  }

  if (!task.friendlyName || !check.isLength(task.friendlyName, 1, 40)) {
    fail('friendlyName is a required string <= 40 chars')
  }

  if (!task.instanceNameFormat) {
    fail('instanceNameFormat is required')
  }
}
exports.validateTask = validateTask
// ------------------------------------------------------------------------------

// ------------------------------------------------------------------------------
// package functions
// ------------------------------------------------------------------------------
var importExtension = function (rootDir, extensionPath) {
  mkdir('-p', extensionPath)
  makeOptions['extensionResources'].forEach(function (itemName) {
    var taskSourcePath = path.join(rootDir, itemName)
    var taskDestPath = path.join(extensionPath, itemName)

    if (test('-d', taskSourcePath)) {
      cp('-R', taskSourcePath, taskDestPath)
    } else {
      cp(taskSourcePath, taskDestPath)
    }
  })
}
exports.importExtension = importExtension

var importTasks = function (buildPath, packagePath) {
  assert(buildPath, 'buildPath')
  assert(packagePath, 'packagePath')

  var packageTaskPath = path.join(packagePath, 'Tasks')
  mkdir('-p', packageTaskPath)

  // process each file/folder within the source root
  fs.readdirSync(buildPath).forEach(function (itemName) {
    var taskSourcePath = path.join(buildPath, itemName)
    var taskDestPath = path.join(packageTaskPath, itemName)

    // skip the Common folder
    if (itemName === 'Common') {
      return
    }

    cp('-R', taskSourcePath, taskDestPath)
  })
}
exports.importTasks = importTasks

var updateManifests = function (extensionPath) {
  var extensionManifestPath = path.join(extensionPath, 'extension-manifest.json')
  var extensionManifest = JSON.parse(fs.readFileSync(extensionManifestPath))

  var tasksPath = path.join(extensionPath, 'Tasks')
  fs.readdirSync(tasksPath).forEach(function (itemName) {
    var taskJsonPath = path.join(tasksPath, itemName, 'task.json');
    var taskManifest = JSON.parse(fs.readFileSync(taskJsonPath))
    taskManifest.version.Major = semver.major(extensionManifest.version)
    taskManifest.version.Minor = semver.minor(extensionManifest.version)
    taskManifest.version.Patch = semver.patch(extensionManifest.version)
    taskManifest.helpMarkDown = 'Version: ' + extensionManifest.version + '. [More Information](http://redirect.sonarsource.com/doc/install-configure-scanner-tfs-ts.html)'
    fs.writeFileSync(taskJsonPath, JSON.stringify(taskManifest, null, 2));
  })
}
exports.updateManifests = updateManifests

var createPackage = function (packagePath, distPath) {
  assert(packagePath, 'packagePath')
  assert(distPath, 'distPath')

  run('tfx extension create --root ' + packagePath + ' --manifest-globs extension-manifest.json --outputPath ' + distPath + ' --no-prompt')
}
exports.createPackage = createPackage
// ------------------------------------------------------------------------------