using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoedaApi.DTO;
using MoedaApi.Repository;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MoedaApi.Routine
{
    public class CotacaoRoutine : IHostedService, IDisposable
    {
        private readonly ILogger<CotacaoRoutine> _logger;
        private Timer _timer;
        private List<DadosMoedaDTO> _listMoedas;
        private List<DadosCotacaoDTO> _listCotacoes;
        string _rootDirectory;

        // set rootDirectory and get ILogger with DIP
        public CotacaoRoutine(ILogger<CotacaoRoutine> logger)
        {
            _logger = logger;
            _rootDirectory = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)))).Split(@"file:\")[1];
        }

        // internal function to get information from DadosMoeda.csv and load in memory
        internal Task<List<DadosMoedaDTO>> readListMoedas(CancellationToken stoppingToken)
        {
            return Task.Run(() =>
            {
                bool first = true;

                using (var reader = new StreamReader(_rootDirectory+@"\csv\DadosMoeda.csv"))
                {
                    _listMoedas = new List<DadosMoedaDTO>();
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (!first)
                        {
                            var values = line.Split(';');
                            DadosMoedaDTO dado = new DadosMoedaDTO()
                            {
                                Id_Moeda = values[0],
                                Data = DateTime.ParseExact(values[1],"yyyy-MM-dd", CultureInfo.CreateSpecificCulture("pt-BR")).Date
                            };
                            _listMoedas.Add(dado);
                        }
                        first = false;
                    }
                }
                return _listMoedas;
            }, stoppingToken);
        }

        // internal function to get information from DadosCotacao.csv and load in memory
        internal Task<List<DadosCotacaoDTO>> readListCotacoes(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                bool first = true;

                using (var reader = new StreamReader(_rootDirectory + @"\csv\DadosCotacao.csv"))
                {
                    _listCotacoes = new List<DadosCotacaoDTO>();
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        if (!first)
                        {
                            var values = line.Split(';');
                            DadosCotacaoDTO dado = new DadosCotacaoDTO()
                            {
                                Vlr_Cotacao = Convert.ToDecimal(values[0].Replace(",", "."), CultureInfo.InvariantCulture),
                                Cod_cotacao = Convert.ToInt32(values[1]),
                                Data_cotacao = DateTime.ParseExact(values[2],"dd/MM/yyyy", CultureInfo.CreateSpecificCulture("pt-BR")).Date
                            };
                            _listCotacoes.Add(dado);
                        }
                        first = false;
                    }
                }
                return _listCotacoes;
            }, cancellationToken);
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            // execute tasks only one time, for performance purposes
            var task1 = readListCotacoes(stoppingToken);
            var task2 = readListMoedas(stoppingToken);
            // wait for all tasks to end
            Task.WhenAll(task1,task2);
            // get results
            _listCotacoes = task1.Result;
            _listMoedas = task2.Result;
            _logger.LogInformation("Iniciando serviço de gerar csv");
            // schedule every 2 minutes 
            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromMinutes(2));
            return Task.CompletedTask;
        }

        private async void DoWork(object state)
        {
            _logger.LogInformation(
                "Executando serviço de gerar csv em: " + DateTime.Now.ToString());
            DateTime iniTime = DateTime.Now;
            string fileName = "Resultado_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".csv";
            string filePath = _rootDirectory + @"\csv\"+fileName;
            List<MoedaDTO> moedas = new List<MoedaDTO>();
            // try to do getItemFila by request in the api
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var uri = "https://localhost:44361/moeda";
                    using (var response = await httpClient.GetAsync(uri))
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();
                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            moedas.AddRange(JsonConvert.DeserializeObject<List<MoedaDTO>>(apiResponse));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(
                "Timeout no request em : " + DateTime.Now.ToString());
            }
            
            // logic to get the info from the in memory lists using LINQ
            Auxiliar aux = new Auxiliar();
            StringBuilder sb = new StringBuilder();
            string header = "ID_MOEDA,DATA_REF,VLR_COTACAO";
            string delimiter = ",";
            sb.AppendLine(header);
            foreach (var moeda in moedas)
            {
                var a = aux.dicionario[moeda.Moeda];
                var moedasResult = _listMoedas.Where(x => x.Id_Moeda == moeda.Moeda && (x.Data.Date >= moeda.Data_inicio.Date && x.Data.Date <= moeda.Data_fim.Date));

                foreach (var item in moedasResult)
                {
                    string result = "";
                    var cotacao = _listCotacoes.Where(x => x.Cod_cotacao == aux.dicionario[moeda.Moeda] && x.Data_cotacao.Date == item.Data.Date).First();
                    result += item.Id_Moeda + delimiter + item.Data.ToShortDateString() + delimiter + cotacao.Vlr_Cotacao;
                    sb.AppendLine(result);
                }
            }
            if (moedas.Count > 0)
            {
                File.WriteAllText(filePath, sb.ToString());
            }
            DateTime fimTime = DateTime.Now;

            var finalTime = fimTime - iniTime;
            _logger.LogInformation("Decorrido " + finalTime.TotalSeconds + "segundos na rotina");
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Parando servico de gerar csv");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
