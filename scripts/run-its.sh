#!/usr/bin/env bash
set -euo pipefail

: "${TEST_INCLUDE:?TEST_INCLUDE environment variable is required but was not set.}"

SQ_VERSION="${SQ_VERSION:-LATEST_RELEASE}"
SQ_EDITION="${SQ_EDITION:-DEVELOPER}"

DOTNET_VERSION="${DOTNET_VERSION:-DEV}"
DRE_VERSION="${DRE_VERSION:-LATEST_RELEASE}"
CFAMILY_VERSION="${CFAMILY_VERSION:-LATEST_RELEASE}"
XML_VERSION="${XML_VERSION:-LATEST_RELEASE}"
CSS_VERSION="${CSS_VERSION:-NONE}"
JAVASCRIPT_VERSION="${JAVASCRIPT_VERSION:-LATEST_RELEASE}"
PLSQL_VERSION="${PLSQL_VERSION:-LATEST_RELEASE}"
PYTHON_VERSION="${PYTHON_VERSION:-LATEST_RELEASE}"
PHP_VERSION="${PHP_VERSION:-LATEST_RELEASE}"
IAC_VERSION="${IAC_VERSION:-NONE}"
IAC_ENTERPRISE_VERSION="${IAC_ENTERPRISE_VERSION:-LATEST_RELEASE}"
JAVA_VERSION="${JAVA_VERSION:-LATEST_RELEASE}"
TEXT_VERSION="${TEXT_VERSION:-LATEST_RELEASE}"
RUBY_VERSION="${RUBY_VERSION:-LATEST_RELEASE}"
GO_VERSION="${GO_VERSION:-NONE}"
GO_ENTERPRISE_VERSION="${GO_ENTERPRISE_VERSION:-LATEST_RELEASE}"
TSQL_VERSION="${TSQL_VERSION:-LATEST_RELEASE}"
GO_GROUP_ID="${GO_GROUP_ID:-org.sonarsource.go}"

if [ -n "${JDK_HOME_VAR:-}" ]; then
  export JAVA_HOME="${!JDK_HOME_VAR}"
fi

MVN_ARGS=(
  -f its -B -e verify
  -Denable-repo=qa
  "-Dtest=${TEST_INCLUDE}"
  "-Dsonar.runtimeVersion=${SQ_VERSION}"
  "-Dsonar.sonarQubeEdition=${SQ_EDITION}"
  "-Dsonar.csharpplugin.version=${DOTNET_VERSION}"
  "-Dsonar.vbnetplugin.version=${DOTNET_VERSION}"
  "-Dsonar.dreplugin.version=${DRE_VERSION}"
  "-Dsonar.cfamilyplugin.version=${CFAMILY_VERSION}"
  "-Dsonar.xmlplugin.version=${XML_VERSION}"
  "-Dsonar.css.version=${CSS_VERSION}"
  "-Dsonar.javascriptplugin.version=${JAVASCRIPT_VERSION}"
  "-Dsonar.plsqlplugin.version=${PLSQL_VERSION}"
  "-Dsonar.pythonplugin.version=${PYTHON_VERSION}"
  "-Dsonar.phpplugin.version=${PHP_VERSION}"
  "-Dsonar.iacplugin.version=${IAC_VERSION}"
  "-Dsonar.iacplugin-enterprise.version=${IAC_ENTERPRISE_VERSION}"
  "-Dsonar.javaplugin.version=${JAVA_VERSION}"
  "-Dsonar.textplugin.version=${TEXT_VERSION}"
  "-Dsonar.rubyplugin.version=${RUBY_VERSION}"
  "-Dsonar.goplugin.version=${GO_VERSION}"
  "-Dsonar.goplugin-enterprise.version=${GO_ENTERPRISE_VERSION}"
  "-Dsonar.tsqlplugin.version=${TSQL_VERSION}"
  "-Dgo.groupid=${GO_GROUP_ID}"
)
if [ "${MSBUILD_PATH_VAR+set}" = "set" ]; then
  MSBUILD_PATH="${MSBUILD_PATH_VAR:+${!MSBUILD_PATH_VAR}}"
  MVN_ARGS+=(
    "-Dmsbuild.path=$MSBUILD_PATH"
    "-Dmsbuild.platformtoolset=v140"
    "-Dmsbuild.windowssdk=10.0.17763.0"
  )
fi

mvn "${MVN_ARGS[@]}"
