// parse command line options
var minimist = require('minimist');
var mopts = {
    string: [
        'publisher'
    ]
};
var options = minimist(process.argv, mopts);

// remove well-known parameters from argv before loading make,
// otherwise each arg will be interpreted as a make target
process.argv = options._;

// modules
require('shelljs/make')
var fs = require('fs')
var path = require('path')
var semver = require('semver')
var utils = require('./make-utils')

// global paths
var buildPath = path.join(__dirname, '_build', 'Tasks')
var commonPath = path.join(__dirname, '_build', 'Tasks', 'Common')
var packagePath = path.join(__dirname, '_package')
var distPath = path.join(__dirname, '_dist')

// node min version
var minNodeVer = '4.0.0'
if (semver.lt(process.versions.node, minNodeVer)) {
  utils.fail('requires node >= ' + minNodeVer + '.  installed: ' + process.versions.node)
}

// add node modules .bin to the path so we can dictate version of tsc etc...
var binPath = path.join(__dirname, 'node_modules', '.bin');
if (!test('-d', binPath)) {
    fail('node modules bin not found.  ensure npm install has been run.');
}
utils.addPath(binPath);

// list of tasks
var taskList = JSON.parse(fs.readFileSync(path.join(__dirname, 'make-options.json'))).tasks

//
// make commands
//

// removes the _build folder
// ex: node make.js clean
target.clean = function () {
  rm('-Rf', path.join(__dirname, '_build'))
  mkdir('-p', buildPath)
  utils.banner('Clean successful', true)
}

// compile every tasks and every common modules
// ex: node make.js build
target.build = function () {
  target.clean()

  utils.ensureTool('tsc', '--version', 'Version 2.1.4')
  utils.ensureTool('npm', '--version', function (output) {
    if (semver.lt(output, '3.0.0')) {
      utils.fail('expected 3.0.0 or higher')
    }
  })

  taskList.forEach(function (taskName) {
    utils.banner('Building: ' + taskName)

    // check task folder exists
    var taskPath = path.join(__dirname, 'Tasks', taskName)
    utils.ensureExists(taskPath)

    // load and check task.json
    var taskJsonPath = path.join(taskPath, 'task.json')
    if (!utils.test('-f', taskJsonPath)) {
      utils.fail('no task.json file found for the task')
    }
    var taskDef = require(taskJsonPath)
    utils.validateTask(taskDef)

    var outDir = path.join(buildPath, taskDef.name)

    utils.mkdir('-p', outDir)

    // load the make.json of the task if it exists
    var taskMakePath = path.join(taskPath, 'make.json')
    var taskMake = utils.test('-f', taskMakePath) ? require(taskMakePath) : {}

    if (taskMake.hasOwnProperty('externals')) {
      console.log('Getting task externals')
      utils.getExternals(taskMake.externals, outDir)
    }

    if (taskMake.hasOwnProperty('common')) {
      var common = taskMake['common']

      common.forEach(function (mod) {
        var modPath = path.join(taskPath, mod['module'])
        var modName = path.basename(modPath)
        var modOutDir = path.join(commonPath, modName)

        if (!utils.test('-d', modOutDir)) {
          utils.banner('Building module ' + modPath, true)
          utils.mkdir('-p', modOutDir)

          // npm install and compile
          if ((mod.type === 'node' && mod.compile === true) || utils.test('-f', path.join(modPath, 'tsconfig.json'))) {
            utils.buildNodeTask(modPath, modOutDir)
          }

          // copy default resources and any additional resources defined in the module's make.json
          console.log();
          console.log('> copying module resources');
          var modMakePath = path.join(modPath, 'make.json');
          var modMake = utils.test('-f', modMakePath) ? utils.require(modMakePath) : {};
          utils.copyTaskResources(modMake, modPath, modOutDir);

          // get externals
          if (modMake.hasOwnProperty('externals')) {
            console.log('Getting module externals');
            utils.getExternals(modMake.externals, modOutDir);
          }
        }

        // npm install the common module to the task dir
        if (mod.type === 'node' && mod.compile === true) {
          utils.mkdir('-p', path.join(taskPath, 'node_modules'))
          utils.rm('-Rf', path.join(taskPath, 'node_modules', modName))
          var originalDir = pwd()
          utils.cd(taskPath)
          utils.run('npm install ' + modOutDir)
          utils.cd(originalDir)
        }
      })

      // build Node task
      utils.buildNodeTask(taskPath, outDir)

      // copy default resources and any additional resources defined in the task's make.json
      console.log()
      console.log('> copying task resources')
      utils.copyTaskResources(taskMake, taskPath, outDir)
    }
  })

  utils.banner('Build successful', true)
}

// run unit tests and create coverage report
// ex: node make.js test
target.test = function () {
}

// create the extension package (.vsix)
// ex: node make.js package
target.package = function () {
  utils.rm('-Rf', packagePath)

  utils.ensureTool('tfx', 'version', function (output) {
    if (!output.endsWith('Version 0.3.45')) {
      utils.fail('expected version 0.3.45')
    }
  })

  var extensionPath = path.join(packagePath, 'SonarQube')
  utils.importExtension(__dirname, extensionPath)
  utils.importTasks(buildPath, extensionPath)
  utils.updateManifests(extensionPath)

  utils.createPackage(extensionPath, distPath)

  utils.banner('Package successful', true)
}
