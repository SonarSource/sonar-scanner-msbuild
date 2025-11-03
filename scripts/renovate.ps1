git rev-parse --show-toplevel | pushd
git checkout master
gh pr list --app "renovate" --state "open" --json "number,headRefName" | ConvertFrom-Json | foreach { 
    $n = $_.number
    $b = $_.headRefName
    Write-Host "Updating PR #$n"
    gh pr update-branch $n
    git branch -D $b
    gh co $n
    dotnet restore --force-evaluate
    git add -u
    git commit -m "Update packages.lock.json"
    git push
    gh pr review --approve -b "LGTM"
    gh pr comment -b "/azp run"
}
popd