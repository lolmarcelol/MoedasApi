using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MoedaApi.DTO
{
    public class MoedaDTO
    {
        public string Moeda { get; set; }
        public DateTime Data_inicio { get; set; }
        public DateTime Data_fim { get; set; }
    }
}
