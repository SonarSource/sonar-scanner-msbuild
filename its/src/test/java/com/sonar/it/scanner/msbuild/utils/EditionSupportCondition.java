package com.sonar.it.scanner.msbuild.utils;

import com.sonar.it.scanner.msbuild.sonarqube.ServerTests;
import com.sonar.orchestrator.version.Version;
import java.util.Arrays;
import org.junit.jupiter.api.extension.ConditionEvaluationResult;
import org.junit.jupiter.api.extension.ExecutionCondition;
import org.junit.jupiter.api.extension.ExtensionContext;

public class EditionSupportCondition implements ExecutionCondition {
  @Override
  public ConditionEvaluationResult evaluateExecutionCondition(ExtensionContext context) {
    final var method = context.getRequiredTestMethod();
    final var enableOnEdition = method.getDeclaredAnnotation(EnableOnEdition.class);
    final var disableOnEdition = method.getDeclaredAnnotation(DisableOnEdition.class);
    if (enableOnEdition == null && disableOnEdition == null) {
      return ConditionEvaluationResult.enabled("Test enabled");
    }
    var serverEdition = ServerTests.ORCHESTRATOR.getServer().getEdition();
    if (enableOnEdition != null) {
      var supported = enableOnEdition.value();
      return Arrays.stream(supported).anyMatch(x -> x == serverEdition)
        ? ConditionEvaluationResult.enabled("Edition " + serverEdition.toString() + " is supported.")
        : ConditionEvaluationResult.disabled("Edition " + serverEdition.toString() + " is not supported.");
    }
    var unsupported = disableOnEdition.value();
    return Arrays.stream(unsupported).anyMatch(x -> x == serverEdition)
      ? ConditionEvaluationResult.disabled("Edition " + serverEdition.toString() + " is not supported.")
      : ConditionEvaluationResult.enabled("Edition " + serverEdition.toString() + " is supported.");
  }
}
