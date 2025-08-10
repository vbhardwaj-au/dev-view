/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace Entities.DTOs.Analytics
{
    public class CommitPunchcardDto
    {
        public int DayOfWeek { get; set; } // 0=Sunday, 1=Monday, ..., 6=Saturday
        public int HourOfDay { get; set; } // 0-23
        public int CommitCount { get; set; }
    }
} 