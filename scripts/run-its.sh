#!/usr/bin/env bash
# Shared IT runner for the Windows/Linux/macOS ITs jobs; each caller resolves its own per-matrix-leg
# values into the env vars read below (job-specific data), while the ~18 plugin-version defaults and
# the mvn invocation shape (logic common to all three jobs) live here.
set -euo pipefail

: "${TEST_INCLUDE:?TEST_INCLUDE environment variable is required but was not set.}"

# SQ_VERSION defaults to LATEST_RELEASE when unset/empty, matching the original Windows job's
# "${{ matrix.sqVersion || 'LATEST_RELEASE' }}" fallback - GH Actions' || treats "" as falsy too, so Windows's
# own Cloud/Others legs (which explicitly set sqVersion: "") already resolved to LATEST_RELEASE today, not empty.
# Inert for Cloud/Others either way (sonar.runtimeVersion is only read by the sonarqube-package tests those legs'
# -Dtest filters never select), so this is a safe, uniform default for every caller.
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

# EXTRA_TEST_EXCLUSIONS is caller-supplied (e.g. Windows's legs pass ",!**/CodeCoverageTest,!**/TelemetryTest");
# Linux/macOS never set it, so -Dtest stays a clean pass-through of TEST_INCLUDE.
TEST_VALUE="${TEST_INCLUDE}${EXTRA_TEST_EXCLUSIONS:-}"

# JDK_HOME_VAR holds the NAME of another env var (e.g. "JAVA_HOME_17_X64"), the same bash indirection the original
# its_windows job used inline; Linux/macOS never set JDK_HOME_VAR and keep mise's ambient JAVA_HOME instead.
if [ -n "${JDK_HOME_VAR:-}" ]; then
  export JAVA_HOME="${!JDK_HOME_VAR}"
fi

# Built as one array (rather than a separate MSBUILD_ARGS appended at the call site) because expanding an empty
# array under "set -u" throws "unbound variable" on bash 3.2 - the fixed system /bin/bash on macOS runners (Apple
# never shipped bash 4+ for licensing reasons) - even though newer bash (Linux, Windows git-bash) handles it fine.
# Since MVN_ARGS always has the base args below, it's never empty, so this bug can't trigger.
MVN_ARGS=(
  -f its -B -e verify
  -Denable-repo=qa
  "-Dtest=${TEST_VALUE}"
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

# Presence, not emptiness, gates these flags: Windows's "Others" leg sets MSBUILD_PATH_VAR="" and still expects
# them (with an empty path); Linux/macOS never set MSBUILD_PATH_VAR at all, so they must never get them - the IT
# harness only reads msbuild.* on Windows anyway.
if [ "${MSBUILD_PATH_VAR+set}" = "set" ]; then
  MSBUILD_PATH="${MSBUILD_PATH_VAR:+${!MSBUILD_PATH_VAR}}"
  MVN_ARGS+=(
    "-Dmsbuild.path=$MSBUILD_PATH"
    "-Dmsbuild.platformtoolset=v140"
    "-Dmsbuild.windowssdk=10.0.17763.0"
  )
fi

mvn "${MVN_ARGS[@]}"
