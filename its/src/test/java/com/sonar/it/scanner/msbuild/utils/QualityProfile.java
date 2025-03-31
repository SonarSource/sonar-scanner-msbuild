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

// The quality profile files can be found in the /qualityProfiles directory
// To add a new quality profile:
// * Create a file named $ProfileName$.xml in /qualityProfiles
// * Make sure the profile name and the file name match: <profile><name>$ProfileName$</name></profile>
// * Add here: $ProfileName$("$language$");
public enum QualityProfile {
  CS_Empty("cs"),
  CS_S1134("cs"),
  CS_S1134_S2699("cs"),
  CS_S1134_S125("cs"),
  CS_S107("cs"),
  CPP_S106("cs"),
  VB_S3385_S125("vbnet"),
  VB_S3385_S2358("vbnet");

  public final String language;

  QualityProfile(String language) {
    this.language = language;
  }

  public static List<String> allProfiles() {
    return Arrays.stream(QualityProfile.class.getFields()).filter(x -> x.isEnumConstant()).map(x -> x.getName()).toList();
  }
}
