/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
package com.sonar.it.scanner.msbuild.utils;

import java.util.Arrays;
import java.util.List;

public class QualityProfiles {
  public static final String CS_Empty = "CS_Empty";
  public static final String CS_S1134 = "CS_S1134";
  public static final String CS_S1134_S2699 = "CS_S1134_S2699";
  public static final String CS_S1134_S125 = "CS_S1134_S125";
  public static final String CS_S107 = "CS_S107";
  public static final String CPP_S106 = "CPP_S106";
  public static final String VB_S3385_S125 = "VB_S3385_S125";
  public static final String VB_S3385_S2358 = "VB_S3385_S2358";

  public static List<String> AllProfiles() throws IllegalAccessException
  {
    return Arrays.stream(QualityProfiles.class.getFields()).map(x -> {
      try {
        return (String)x.get(null);
      } catch (IllegalAccessException e) {
        throw new RuntimeException(e);
      }
    }).toList();
  }
}
