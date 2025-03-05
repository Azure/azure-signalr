// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClientResultSample.Pages
{
    public class ChatsModel : PageModel
    {
        private readonly ILogger<ChatsModel> _logger;

        public ChatsModel(ILogger<ChatsModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {

        }
    }
}