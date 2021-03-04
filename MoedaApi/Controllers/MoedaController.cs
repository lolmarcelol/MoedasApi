using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MoedaApi.DTO;
using Newtonsoft.Json;

namespace MoedaApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MoedaController : ControllerBase
    {
        private readonly ILogger<MoedaController> _logger;
        readonly IMemoryCache _cache;
        readonly IStringLocalizer<MoedaController> _localizer;

        public MoedaController(ILogger<MoedaController> logger, IMemoryCache memoryCache, IStringLocalizer<MoedaController> localizer)
        {
            _logger = logger;
            _cache = memoryCache;
            _localizer = localizer;
        }

        internal int GetLastKeyInserted()
        {
            string json;
            int count = 0;
            while (_cache.TryGetValue(count.ToString(), out json))
            {
                count++;
            }
            return count;
        }

        [HttpPost]
        public ActionResult<bool> AddItemFila(List<MoedaDTO> _dto)
        {
            // find last inserted
            int lastkeyInserted = GetLastKeyInserted();
            // set cache for 300 days
            var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromDays(300));
            // Save data in cache.
            _cache.Set(lastkeyInserted.ToString(), JsonConvert.SerializeObject(_dto), cacheEntryOptions);
            return Ok(_dto);
        }

        [HttpGet]
        public ActionResult<bool> GetItemFila()
        {
            // get the position in cache
            int lastkeyInserted = GetLastKeyInserted() - 1;
            string json;
            // rescue the last item added
            if (_cache.TryGetValue(lastkeyInserted.ToString(), out json))
            {
                // remove from cache
                _cache.Remove(lastkeyInserted.ToString());
                return Ok(JsonConvert.DeserializeObject<List<MoedaDTO>>(json));
            }
            else
            {
                return Unauthorized(new { msg = _localizer["erroCache"].Value });
            }

        }
    }
}
