/*
 * DevView - .NET 9 Bitbucket Analytics Solution
 * Copyright (c) 2025 Vikas Bhardwaj
 * 
 * This project is licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace Entities.DTOs.Commits
{
    public class CommitResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public int CommitsProcessed { get; set; }
    }
} 