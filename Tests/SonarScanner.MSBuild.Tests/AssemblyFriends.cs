/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2021 SonarSource SA
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

using System.Runtime.CompilerServices;
#if CodeSigned
[assembly: InternalsVisibleTo("SonarScanner.PreProcessor.Tests, PublicKey=002400000480000094000000060200000024000052534131000400000100010029d15920332ded89851197f2ef16bfebd9cfc0acd7e0f3f5bbdc0d0ae03e7a893820e693e2ee9d886b362da373a6cd69e6041894fba4ea73b4c1ea31d1d6f2bd2b5a108f8863d0e01d52c58f29949719015b2889cc9f5057d7a802617d11f4c344dba9aae6d262b79c5220987b08ec0bfd9e39b0bb008441fa37b3f3b89814b8")]
#else
[assembly: InternalsVisibleTo("SonarScanner.PreProcessor.Tests")]
#endif
