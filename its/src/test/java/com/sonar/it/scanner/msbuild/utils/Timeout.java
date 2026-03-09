/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource Sàrl
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

public enum Timeout {
  ONE_MINUTE(1),
  TWO_MINUTES(2),
  FIVE_MINUTES(5),
  TEN_MINUTES(10); // https://dev.azure.com/sonarsource/399fb241-ecc7-4802-8697-dcdd01fbb832/_build/results?buildId=135493&view=logs&jobId=55d7b6d8-8660-5e3b-2c0a-f098d2482921

  public final long miliseconds;

  Timeout(int minutes) {
    this.miliseconds = minutes * 60 * 1000;
  }
}
