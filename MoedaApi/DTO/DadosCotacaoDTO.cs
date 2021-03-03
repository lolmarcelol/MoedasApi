using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MoedaApi.DTO
{
    public class DadosCotacaoDTO
    {
        public decimal Vlr_Cotacao { get; set; }
        public int Cod_cotacao { get; set; }
        public DateTime Data_cotacao { get; set; }
    }
}
