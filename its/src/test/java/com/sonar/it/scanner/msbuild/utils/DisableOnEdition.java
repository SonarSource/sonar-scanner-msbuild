package com.sonar.it.scanner.msbuild.utils;

import com.sonar.orchestrator.container.Edition;
import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;
import org.junit.jupiter.api.extension.ExtendWith;

@Retention(RetentionPolicy.RUNTIME)
@Target({ElementType.METHOD, ElementType.TYPE})
@ExtendWith(EditionSupportCondition.class)
public @interface DisableOnEdition {
  Edition[] value();
}
