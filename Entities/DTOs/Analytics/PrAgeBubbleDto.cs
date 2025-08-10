/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace Entities.DTOs.Analytics
{
    public class PrAgeBubbleDto
    {
        public int AgeInDays { get; set; }
        public int NumberOfPRs { get; set; }
        public string? RepositoryName { get; set; }
        public string? RepositorySlug { get; set; }
        public string? Workspace { get; set; }
    }
} 