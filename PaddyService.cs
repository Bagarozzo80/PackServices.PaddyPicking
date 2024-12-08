using PackServices.ReadyPro.Data.Models;
using PackServices.ReadyPro.Services;
using ReadyProLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using UtilityLib;

namespace PaddyPicking
{
    public partial class PaddyService : ServiceBase
    {
        private readonly IPaddyPickingService _paddyPickingService;
        //private readonly IPaddyPickingService _paddyPickingScaricoService;
        private readonly IDocumentService _documentService;

        private System.Timers.Timer timerCheckPaddy;
        private System.Timers.Timer timerCheckEntity;
        private System.Timers.Timer timerCheckEntityUnloading;
        private System.Timers.Timer timerEAN;

        Utilita readyUtilita = new Utilita();

        private Thread _threadCheckEntity, _threadCheckEntityUnloading, _threadEAN, _threadCheckPaddy;

        public PaddyService(
            IPaddyPickingService paddyPickingService,
            IDocumentService documentService)
        {
            InitializeComponent();

            _paddyPickingService = paddyPickingService;
            //_paddyPickingScaricoService = paddyPickingScaricoService;
            _documentService = documentService;

            if (!EventLog.SourceExists(nameof(PaddyService)))
                EventLog.CreateEventSource(nameof(PaddyService), "Application");
        }

        protected override void OnStart(string[] args)
        {
            timerCheckPaddy = new System.Timers.Timer();
            timerCheckPaddy.Interval = 90000;
            timerCheckPaddy.Elapsed += new ElapsedEventHandler(timerCheckPaddy_Elapsed);
            timerCheckPaddy.Enabled = true;

            //timerCheckEntity = new System.Timers.Timer();
            //timerCheckEntity.Interval = 90000;
            //timerCheckEntity.Elapsed += new ElapsedEventHandler(timerCheckEntity_Elapsed);
            //timerCheckEntity.Enabled = true;

            //timerCheckEntityUnloading = new System.Timers.Timer();
            //timerCheckEntityUnloading.Interval = 120000;
            //timerCheckEntityUnloading.Elapsed += new ElapsedEventHandler(timerCheckEntityUnloading_Elapsed);
            //timerCheckEntityUnloading.Enabled = true;

            timerEAN = new System.Timers.Timer();
            timerEAN.Interval = 1000;
            timerEAN.Elapsed += new ElapsedEventHandler(timerEAN_Elapsed);
            timerEAN.Enabled = true;
        }

        protected override void OnStop()
        {

        }

        protected void timerCheckPaddy_Elapsed(object sender, ElapsedEventArgs e)
        {
            DateTime dtProcess = DateTime.Now;

            Control.CheckForIllegalCrossThreadCalls = false;

            _threadCheckPaddy = new Thread(CheckPaddy);
            _threadCheckPaddy.Name = "Paddy Picking - Check Paddy";
            _threadCheckPaddy.IsBackground = true;
            _threadCheckPaddy.Priority = ThreadPriority.Normal;
            _threadCheckPaddy.Start();
        }

        //protected void timerCheckEntity_Elapsed(object sender, ElapsedEventArgs e)
        //{
        //    DateTime dtProcess = DateTime.Now;

        //    Control.CheckForIllegalCrossThreadCalls = false;

        //    _threadCheckEntity = new Thread(CheckEntity);
        //    _threadCheckEntity.Name = "Paddy Picking - Check Entity";
        //    _threadCheckEntity.IsBackground = true;
        //    _threadCheckEntity.Priority = ThreadPriority.Normal;
        //    _threadCheckEntity.Start();
        //}

        //protected void timerCheckEntityUnloading_Elapsed(object sender, ElapsedEventArgs e)
        //{
        //    DateTime dtProcess = DateTime.Now;

        //    Control.CheckForIllegalCrossThreadCalls = false;

        //    _threadCheckEntityUnloading = new Thread(CheckUnloading);
        //    _threadCheckEntityUnloading.Name = "Paddy Picking - Check Entity Unloading";
        //    _threadCheckEntityUnloading.IsBackground = true;
        //    _threadCheckEntityUnloading.Priority = ThreadPriority.Normal;
        //    _threadCheckEntityUnloading.Start();
        //}

        protected void timerEAN_Elapsed(object sender, ElapsedEventArgs e)
        {
            DateTime dtProcess = DateTime.Now;

            Control.CheckForIllegalCrossThreadCalls = false;

            if (dtProcess.Hour == 22 && dtProcess.Minute == 30 && dtProcess.Second == 0)
            {
                _threadEAN = new Thread(UpdateEAN);
                _threadEAN.Name = "Paddy Picking - Update EAN";
                _threadEAN.IsBackground = true;
                _threadEAN.Priority = ThreadPriority.Normal;
                _threadEAN.Start();
            }
        }

        protected void CheckPaddy()
        {
            string dtInizio = DateTime.Now.ToString();

            try
            {
                //leggo gli ordini da inserire sul sistema Paddy Picking
                var list = _paddyPickingService.GetAllPaddyPicking().ToList();
                
                CheckEntity(list.Where(p => p.doctipo_id == "OC").ToList());
                CheckUnloading(list.Where(p => p.doctipo_id == "OF").ToList());
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(nameof(PaddyService), "Check Paddy: " + ex.StackTrace, EventLogEntryType.Error);
                CommonUtility.WriteLog(CommonUtility.PathLog, nameof(PaddyService) + " - Check Paddy: " + ex.StackTrace);
            }

            string dtFine = DateTime.Now.ToString();

            EventLog.WriteEntry(nameof(PaddyService), "Check Paddy: Inizio [" + dtInizio + "] | Fine [" + dtFine + "]", EventLogEntryType.Information);
        }

        protected void CheckEntity(List<PackServices.ReadyPro.Data.Models.PaddyPicking> list)
        {
            //string dtInizio = DateTime.Now.ToString();

            //List<int> ordersException = new List<int>();

            List<OrderListModel> orderList = new List<OrderListModel>();
            List<OrderUserModel> orderUserList = new List<OrderUserModel>();

            //UPDATE ERP
            //string SQLUpdate_Bolle = "UPDATE Bolle SET [Consegna urgente] = 0, [ID gruppo] = {0}, [Numero di colli] = {1}, [Aspetto merce] = '{2}' WHERE [ID bolla] = {3};";
            string SQLUpdate_Bolle = "UPDATE Bolle SET [Consegna urgente] = 0, [ID gruppo] = {0}, [Numero di colli] = {1} WHERE [ID bolla] = {2};";
            string SQLUpdate_Bolle2 = "UPDATE [Bolle 2] SET DescrizioneContenuto = '{0}' WHERE [ID bolla] = {1};";
            string SQLUpdate_BolleDetteglio2 = "IF NOT EXISTS (SELECT * FROM [Bolle dettaglio 2] WHERE [ID bolla dettaglio] = {1}) BEGIN INSERT INTO [Bolle dettaglio 2] ([ID bolla dettaglio], RecordStatus, [Descrizione estesa], [Descrizione campo 2], [Descrizione campo 3], [Note linea], [Descrizione prezzo base], [Note arrivo], NoteLinea2, NoteLinea3, IdPreventivoAlfa, DataConsegnaPrevista) VALUES ({1}, NULL, NULL, NULL, NULL, '{0}', NULL, NULL, NULL, NULL, NULL, NULL) END ELSE BEGIN UPDATE [Bolle dettaglio 2] SET [Note linea] = '{0}' WHERE [ID bolla dettaglio] = {1} END";
            string SQLUpdate_MagaMov = "UPDATE PP_magamov SET erp_update = {0} WHERE id = {1};";
            string SQLInsert_ListaPaddy = "IF NOT EXISTS (SELECT * FROM [Bolle blocchi] WHERE [ID bolla] = {0} AND [Nome campo] = 'PADDY_LISTA') BEGIN INSERT INTO [Bolle blocchi] ([ID blocco], RecordStatus, [ID bolla], [Nome campo], [Blocco testo]) VALUES ((SELECT MAX([ID blocco]) + 1 FROM [Bolle blocchi]), '{1}', {2}, 'PADDY_LISTA', '{3}') END";

            int UltimoProgressivo = 0;

            try
            {
                #region Totale oridini per utente

                //elenco degli stati da considerare per l'assegnazione degli ordini
                int[] idsStato = new int[3] { 0, 1, 10 };

                var orderUsers =
                    from users in _paddyPickingService.GetAllPaddyPickingUsers().Where(u => u.assegna_ordini)
                    join orders in _paddyPickingService.GetMagaOrd(idsStato) on users.nome equals orders.lista_id into leftJoin
                    from lj in leftJoin.DefaultIfEmpty()
                    group new { users, lj } by new { users.nome } into g
                    select new { g.Key.nome, Totale = g.Count(x => x.lj != null) };

                foreach (var item in orderUsers)
                    orderUserList.Add(new OrderUserModel { ListaID = item.nome, OrderTotal = item.Totale });

                #endregion

                //leggo gli ordini da inserire sul sistema Paddy Picking
                //var list = _paddyPickingService.GetAllPaddyPicking().Where(p => p.doctipo_id == "OC").ToList();

                int idxRow = 1;
                foreach (PackServices.ReadyPro.Data.Models.PaddyPicking item in list)
                {
                    try
                    {
                        UltimoProgressivo = item.progressivo;

                        //if (ordersException.Contains(item.id))
                        //    continue;

                        //var checkErrors = list.Where(s => s.id == item.id && s.anno == item.anno && s.progressivo == item.progressivo)
                        //    .GroupBy(n => n.articolo_id)
                        //    .Select(n => new { articolo = n.Key, totaleRighe = n.Count() })
                        //    .Where(n => n.totaleRighe > 1)
                        //    .OrderBy(n => n.articolo);

                        //if (checkErrors.Count() > 0) {
                        //    ordersException.Add(item.id);
                        //    EventLog.WriteEntry(nameof(PaddyService), "ORDINE SCARTATO : " + item.progressivo, EventLogEntryType.Error);
                        //    continue;
                        //}

                        //Leggo l'utente da assegnare all'ordine, prendendo quello con meno ordini
                        var user = orderUserList.OrderBy(o => o.OrderTotal).FirstOrDefault();

                        string[] splitDestinazione = item.destinazione.Split('|');
                        string[] splitDestinazioneBolla = string.IsNullOrEmpty(item.destinazione_bolla) ? new string[0] : item.destinazione_bolla.Replace("$", string.Empty).Replace("{", string.Empty).Replace("}", string.Empty).Split('|');

                        string destinazioneID = item.destinazione_id == 0 ? item.destinazione_idanagrafica.ToString() : string.Concat(item.destinazione_idanagrafica.ToString(), "_", item.destinazione_id.ToString());

                        string ragioneSociale = string.IsNullOrEmpty(splitDestinazione.GetValue(2).ToString()) ? splitDestinazione.GetValue(1).ToString() : string.Concat(splitDestinazione.GetValue(1).ToString(), " ", splitDestinazione.GetValue(2).ToString());

                        string destinatario = splitDestinazioneBolla.Length > 1 ?
                            string.IsNullOrEmpty(splitDestinazioneBolla.GetValue(2).ToString()) ? splitDestinazioneBolla.GetValue(1).ToString() : string.Concat(splitDestinazioneBolla.GetValue(1).ToString(), " ", splitDestinazioneBolla.GetValue(2).ToString()) :
                            string.IsNullOrEmpty(splitDestinazione.GetValue(2).ToString()) ? splitDestinazione.GetValue(1).ToString() : string.Concat(splitDestinazione.GetValue(1).ToString(), " ", splitDestinazione.GetValue(2).ToString());

                        //tabella PP_articolo
                        var articolo = _paddyPickingService.GetArticolo(item.articolo_id, item.maga_id);
                        if (articolo is null)
                        {
                            articolo = new PP_articolo
                            {
                                id = item.articolo_id,
                                maga_id = item.maga_id,
                                vano_id = item.vano_id,
                                descr = item.articolo_descr,
                                descrprint = item.articolo_descr,
                                cat = item.articolo_cat,
                                catorder = "",
                                umxpz = (int)item.articolo_qlotto,
                                umisu = item.articolo_um,
                                multipli = 0,
                                glotto = "N",
                                gmatr = "N",
                                note = item.articolo_note,
                                ubicazione = string.IsNullOrEmpty(item.articolo_ubicazione.Trim()) ? "nd" : item.articolo_ubicazione.Trim(),
                                cod_fornitore = string.IsNullOrEmpty(item.cod_fornitore.Trim()) ? "nd" : item.cod_fornitore.Trim()
                            };

                            _paddyPickingService.Create(articolo);
                        }
                        else
                        {
                            string ubicazione = string.IsNullOrEmpty(item.articolo_ubicazione.Trim()) ? "nd" : item.articolo_ubicazione.Trim();
                            string cod_fornitore = string.IsNullOrEmpty(item.cod_fornitore.Trim()) ? "nd" : item.cod_fornitore.Trim();

                            if (!articolo.ubicazione.Equals(ubicazione) ||
                                !articolo.cod_fornitore.Equals(cod_fornitore) ||
                                !articolo.umxpz.Equals((int)item.articolo_qlotto) ||
                                !articolo.descr.Equals(item.articolo_descr) ||
                                !articolo.descrprint.Equals(item.articolo_descr))
                            {

                                articolo.ubicazione = ubicazione;
                                articolo.cod_fornitore = cod_fornitore;
                                articolo.umxpz = (int)item.articolo_qlotto;
                                articolo.descr = item.articolo_descr;
                                articolo.descrprint = item.articolo_descr;

                                _paddyPickingService.Update(articolo);

                                EventLog.WriteEntry(nameof(PaddyService), "UPDATED: " + item.progressivo + "\nArticolo: " + item.articolo_id + "\nUbicazione: " + ubicazione + " | Cod.Fornitore: " + cod_fornitore + "\nUbicazione: " + articolo.ubicazione + " | Cod.Fornitore: " + articolo.cod_fornitore, EventLogEntryType.Warning);
                            }
                        }

                        //tabella PP_maga
                        var maga = _paddyPickingService.GetMaga(item.maga_id, item.articolo_id, item.vano_id, item.maga_lotto);
                        if (maga is null)
                        {
                            maga = new PP_maga
                            {
                                maga_id = item.maga_id,
                                articolo_id = item.articolo_id,
                                vano_id = item.vano_id,
                                maga_lotto = item.maga_lotto,
                                giac = 0
                            };

                            _paddyPickingService.Create(maga);
                        }

                        //tabella PP_destinazione
                        var destinazione = _paddyPickingService.GetDestinazione(destinazioneID);
                        if (destinazione is null)
                        {
                            destinazione = new PP_destinazione
                            {
                                id = destinazioneID,
                                tipo = splitDestinazioneBolla.Length > 1 ? "C" : splitDestinazione.GetValue(0).ToString(),
                                ragso = ragioneSociale,
                                piva = item.destinazione_piva,
                                cf = item.destinazione_cf,
                                destinatario = destinatario.ToUpper(),
                                nazione = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(8).ToString() : splitDestinazione.GetValue(3).ToString(),
                                indirizzo = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(3).ToString() : splitDestinazione.GetValue(4).ToString(),
                                citta = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(6).ToString() : splitDestinazione.GetValue(5).ToString(),
                                provincia = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(7).ToString() : splitDestinazione.GetValue(6).ToString(),
                                cap = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(5).ToString() : splitDestinazione.GetValue(7).ToString(),
                                note = "",
                                area = "",
                                areaorder = 0,
                                prioritario = false
                            };

                            _paddyPickingService.Create(destinazione);
                        }
                        else
                        {
                            //UNCLE BETO SRL
                            if (destinazioneID == "20521")
                            {
                                var destinazioneAlternativa = _paddyPickingService.GetDestinazione(destinazioneID + "_", destinatario.ToUpper(), true);
                                if (destinazioneAlternativa is null)
                                {
                                    var destinazioneMax = _paddyPickingService.GetAllDestinazione(destinazioneID + "_");

                                    if (destinazioneMax is null || destinazioneMax.Count.Equals(0))
                                        destinazioneID += "_1";
                                    else
                                    {
                                        int maxID = int.Parse(destinazioneMax.Select(d => int.Parse(d.id.Split('_').GetValue(1).ToString())).Max().ToString());
                                        destinazioneID += "_" + (maxID + 1).ToString();
                                    }

                                    destinazioneAlternativa = new PP_destinazione
                                    {
                                        id = destinazioneID,
                                        tipo = splitDestinazioneBolla.Length > 1 ? "C" : splitDestinazione.GetValue(0).ToString(),
                                        ragso = ragioneSociale,
                                        piva = item.destinazione_piva,
                                        cf = item.destinazione_cf,
                                        destinatario = destinatario.ToUpper(),
                                        nazione = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(8).ToString() : splitDestinazione.GetValue(3).ToString(),
                                        indirizzo = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(3).ToString() : splitDestinazione.GetValue(4).ToString(),
                                        citta = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(6).ToString() : splitDestinazione.GetValue(5).ToString(),
                                        provincia = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(7).ToString() : splitDestinazione.GetValue(6).ToString(),
                                        cap = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(5).ToString() : splitDestinazione.GetValue(7).ToString(),
                                        note = "",
                                        area = "",
                                        areaorder = 0,
                                        prioritario = false
                                    };

                                    _paddyPickingService.Create(destinazioneAlternativa);
                                }

                                //imposto la destinazione uguale a quella alternativa per permttere il corretto inserimento nell'ordine
                                destinazione = destinazioneAlternativa;
                            }
                        }

                        //tabella PP_magaord
                        var magaOrd = _paddyPickingService.GetMagaOrd(item.id, item.anno, item.progressivo, item.nriga);
                        if (magaOrd is null)
                        {
                            string assignedList = AssegnaLista(orderList, item, list, user, destinazioneID);

                            //valorizzo l'array contenente l'associazione ordine e lista di assegnazione
                            orderList.Add(new OrderListModel(item.id, assignedList));

                            magaOrd = new PP_magaord
                            {
                                id = item.id,
                                anno = item.anno,
                                progressivo = item.progressivo,
                                nriga = (int)item.nriga,
                                articolo_id = item.articolo_id,
                                causale_id = item.causale_id,
                                doctipo_id = item.doctipo_id,
                                sezionale = item.sezionale,
                                maga_lotto = item.maga_lotto,
                                destinazione_id = destinazione is null ? destinazioneID : destinazione.id,
                                dataconsegna = item.dataconsegna,
                                qnt = (int)item.qnt,
                                lista_id = assignedList,
                                vano_id = item.vano_id,
                                vettore_id = item.vettore_id,
                                pagina = item.pagina,
                                info_row = item.info_row,
                                info_full = item.info_full,
                                pausable = item.pausable == 1 ? true : false,
                                maga_id = item.maga_id,
                                movtype = item.movtype,
                                stato = item.stato,
                                ddtinforequired = item.ddtinforequired == 1 ? true : false,
                                data_creazione = DateTime.Now
                            };

                            _paddyPickingService.Create(magaOrd);

                            EventLog.WriteEntry(nameof(PaddyService), "INSERTED : " + magaOrd.progressivo + " - " + magaOrd.articolo_id + " - " + magaOrd.qnt + " - " + magaOrd.stato, EventLogEntryType.Warning);
                        }
                        else
                        {
                            //if (item.destinazione_idanagrafica == 790257)
                            //    EventLog.WriteEntry(nameof(PaddyService), "Check Entity [" + item.progressivo + "] : " + magaOrd.stato, EventLogEntryType.Warning);

                            //se esiste ed è in stato cancellato(5), elimino la riga sul palmarino per permettere la gestione della missione di magazzino
                            if (magaOrd.stato == 5)
                            {
                                //elimino la missione di magazzino presente a DB
                                _paddyPickingService.Delete(magaOrd);

                                EventLog.WriteEntry(nameof(PaddyService), "DELETED : " + magaOrd.progressivo + " - " + magaOrd.articolo_id + " - " + magaOrd.qnt + " - " + magaOrd.stato, EventLogEntryType.Warning);

                                string assignedList = AssegnaLista(orderList, item, list, user, destinazioneID);

                                //valorizzo l'array contenente l'associazione ordine e lista di assegnazione
                                orderList.Add(new OrderListModel(item.id, assignedList));

                                //magaOrd.articolo_id = item.articolo_id;
                                //magaOrd.causale_id = item.causale_id;
                                //magaOrd.doctipo_id = item.doctipo_id;
                                //magaOrd.sezionale = item.sezionale;
                                //magaOrd.maga_lotto = item.maga_lotto;
                                //magaOrd.destinazione_id = destinazione is null ? destinazioneID : destinazione.id;
                                //magaOrd.dataconsegna = item.dataconsegna;
                                //magaOrd.qnt = (int)item.qnt;
                                //magaOrd.lista_id = assignedList;
                                //magaOrd.vano_id = item.vano_id;
                                //magaOrd.vettore_id = item.vettore_id;
                                //magaOrd.pagina = item.pagina;
                                //magaOrd.info_row = item.info_row;
                                //magaOrd.info_full = item.info_full;
                                //magaOrd.pausable = item.pausable == 1 ? true : false;
                                //magaOrd.maga_id = item.maga_id;
                                //magaOrd.movtype = item.movtype;
                                //magaOrd.stato = item.stato;
                                //magaOrd.ddtinforequired = item.ddtinforequired == 1 ? true : false;
                                //magaOrd.data_creazione = DateTime.Now;

                                //object[] id = new object[4] { magaOrd.id, magaOrd.anno, magaOrd.progressivo, magaOrd.nriga };

                                //_paddyPickingService.Update(id);

                                //PP_magaord magaOrdReCreate = new PP_magaord
                                //{
                                //    id = item.id,
                                //    anno = item.anno,
                                //    progressivo = item.progressivo,
                                //    nriga = (int)item.nriga,
                                //    articolo_id = item.articolo_id,
                                //    causale_id = item.causale_id,
                                //    doctipo_id = item.doctipo_id,
                                //    sezionale = item.sezionale,
                                //    maga_lotto = item.maga_lotto,
                                //    destinazione_id = destinazione is null ? destinazioneID : destinazione.id,
                                //    dataconsegna = item.dataconsegna,
                                //    qnt = (int)item.qnt,
                                //    lista_id = assignedList,
                                //    vano_id = item.vano_id,
                                //    vettore_id = item.vettore_id,
                                //    pagina = item.pagina,
                                //    info_row = item.info_row,
                                //    info_full = item.info_full,
                                //    pausable = item.pausable == 1 ? true : false,
                                //    maga_id = item.maga_id,
                                //    movtype = item.movtype,
                                //    stato = item.stato,
                                //    ddtinforequired = item.ddtinforequired == 1 ? true : false,
                                //    data_creazione = DateTime.Now
                                //};

                                //_paddyPickingService.Create(magaOrdReCreate);
                            }

                        }

                        //tabella PP_articolo_ean
                        var articoloEAN = _paddyPickingService.GetArticoloEAN(item.articolo_id);
                        if (articoloEAN is null)
                        {
                            articoloEAN = new PP_articolo_ean
                            {
                                articolo_id = item.articolo_id,
                                maga_id = item.maga_id,
                                ean = item.articolo_EAN
                            };

                            _paddyPickingService.Create(articoloEAN);
                        }
                        else
                        {
                            if (!articoloEAN.ean.Equals(item.articolo_EAN))
                            {

                                articoloEAN.ean = item.articolo_EAN;

                                _paddyPickingService.Update(articoloEAN);

                                EventLog.WriteEntry(nameof(PaddyService), "Articolo: " + item.articolo_id + "\nEAN: " + item.articolo_EAN, EventLogEntryType.Warning);
                            }
                        }

                        idxRow++;
                    }
                    catch (Exception ex)
                    {
                        EventLog.WriteEntry(nameof(PaddyService), "Check Entity [" + UltimoProgressivo + "] : " + ex.StackTrace, EventLogEntryType.Error);
                        CommonUtility.WriteLog(CommonUtility.PathLog, nameof(PaddyService) + " - Check Entity [" + UltimoProgressivo + "] : " + ex.StackTrace);
                        //CommonUtility.InvioMail(CommonUtility.EMailResponsabileIT, CommonUtility.EMailAmministratore, nameof(PaddyService) + " - Check Entity [" + UltimoProgressivo + "]", ex.Message, string.Empty, null);
                        continue;
                    }
                }

                StringBuilder sbSQL = new StringBuilder();

                #region Aggiornamento campi personalizzati ReadyPro

                //aggiorno il campo PADDY_LISTA su ReadyPro per gli ordini aggiunti                
                foreach (var item in orderList.Select(o => new { o.OrderID, o.LabelList }).Distinct())
                    sbSQL.AppendLine(string.Format(SQLInsert_ListaPaddy, item.OrderID, CommonUtility.RecordStatus(), item.OrderID, item.LabelList));

                if (!string.IsNullOrEmpty(sbSQL.ToString()))
                    readyUtilita.EseguiScript(sbSQL.ToString());

                #endregion

                #region Aggiornamento da Paddy a ReadyPro

                //leggo i movimenti di magazzino confermati
                var listReadyPro = _paddyPickingService.GetMagaMov(0, string.Empty).OrderBy(o => o.progressivo).ThenBy(o => o.magaord_nriga).ToList();

                sbSQL.Clear();
                foreach (PP_magamov item in listReadyPro)
                {
                    //tronco la stringa a 50 perchè su ReadyPro il campo ha quella lunghezza
                    string aspettoMerce = item.note.Trim().Length > 50 ? item.note.Replace("+", "").Replace("'", "''").Trim().Substring(0, 50) : item.note.Replace("+", "").Replace("'", "''").Trim();
                    switch (aspettoMerce.ToUpper())
                    {
                        case "XS":
                            aspettoMerce = "CARTONE 21x30x17";
                            break;
                        case "S":
                            aspettoMerce = "CARTONE 30x30x40";
                            break;
                        case "M":
                            aspettoMerce = "CARTONE 60x50x40";
                            break;
                        case "L":
                            aspettoMerce = "CARTONE 75x65x55";
                            break;
                        case "XL":
                            aspettoMerce = "CARTONE 100x60x60";
                            break;
                        default:
                            break;
                    }

                    int ingombro = 1;
                    int.TryParse(item.ingombro_merce, out ingombro);

                    //aggiorno i dati su Ready solo se la missione di magazzina ha delle quantità confermate
                    if (item.qnt.HasValue && item.qnt.Value > 0)
                    {
                        if (item.vettore_id.Equals("RITIRA IL CLIENTE")) //20: Pronto Ritira il Cliente - 27: Pronto Paddy
                            sbSQL.AppendLine(string.Format(SQLUpdate_Bolle, 27, ingombro.Equals(0) ? 1 : ingombro, int.Parse(item.magaord_id)));
                        else
                            sbSQL.AppendLine(string.Format(SQLUpdate_Bolle, 27, ingombro.Equals(0) ? 1 : ingombro, int.Parse(item.magaord_id)));

                        //if (item.note.Trim().Length > 50)
                        //    sbSQL.AppendLine(string.Format(SQLUpdate_Bolle2, item.note.Replace("+", "").Replace("'", "''").Trim(), int.Parse(item.magaord_id)));

                        sbSQL.AppendLine(string.Format(SQLUpdate_Bolle2, aspettoMerce, int.Parse(item.magaord_id)));
                        sbSQL.AppendLine(string.Format(SQLUpdate_BolleDetteglio2, item.qnt.Value, item.magaord_nriga));
                    }

                    sbSQL.AppendLine(string.Format(SQLUpdate_MagaMov, 1, item.id));
                }

                if (!string.IsNullOrEmpty(sbSQL.ToString()))
                {
                    CommonUtility.WriteLog(CommonUtility.PathLog, "SQL : " + sbSQL.ToString());
                    readyUtilita.EseguiScript(sbSQL.ToString());
                }

                #endregion
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(nameof(PaddyService), "Check Entity [" + UltimoProgressivo + "] : " + ex.StackTrace, EventLogEntryType.Error);
                CommonUtility.WriteLog(CommonUtility.PathLog, nameof(PaddyService) + " - Check Entity [" + UltimoProgressivo + "] : " + ex.StackTrace);
                //CommonUtility.InvioMail(CommonUtility.EMailResponsabileIT, null, nameof(PaddyService) + " - Check Entity [" + UltimoProgressivo + "]", ex.Message, string.Empty, null);
            }

            //string dtFine = DateTime.Now.ToString();

            //EventLog.WriteEntry(nameof(PaddyService), "Check Entity : Inizio [" + dtInizio + "] | Fine [" + dtFine + "]", EventLogEntryType.Information);
        }

        protected void CheckUnloading(List<PackServices.ReadyPro.Data.Models.PaddyPicking> list)
        {
            //string dtInizio = DateTime.Now.ToString();

            //UPDATE ERP
            string SQLUpdate_Bolle = "UPDATE Bolle SET [ID gruppo] = {0} WHERE [ID bolla] = {1};";
            string SQLUpdate_BolleDetteglio2 = "IF NOT EXISTS (SELECT * FROM [Bolle dettaglio 2] WHERE [ID bolla dettaglio] = {1}) BEGIN INSERT INTO [Bolle dettaglio 2] ([ID bolla dettaglio], RecordStatus, [Descrizione estesa], [Descrizione campo 2], [Descrizione campo 3], [Note linea], [Descrizione prezzo base], [Note arrivo], NoteLinea2, NoteLinea3, IdPreventivoAlfa, DataConsegnaPrevista) VALUES ({1}, NULL, NULL, NULL, NULL, '{0}', NULL, NULL, NULL, NULL, NULL, NULL) END ELSE BEGIN UPDATE [Bolle dettaglio 2] SET [Note linea] = '{0}' WHERE [ID bolla dettaglio] = {1} END";
            string SQLUpdate_MagaMov = "UPDATE PP_magamov SET erp_update = {0} WHERE id = {1};";

            int UltimoProgressivo = 0;

            try
            {
                //EventLog.WriteEntry(nameof(PaddyService), "Check Unloading [" + UltimoProgressivo + "] : INIZIO!", EventLogEntryType.Warning);

                //leggo gli ordini da inserire sul sistema Paddy Picking
                //var list = _paddyPickingService.GetAllPaddyPicking().Where(p => p.doctipo_id == "OF").ToList();

                int idxRow = 1;
                foreach (PackServices.ReadyPro.Data.Models.PaddyPicking item in list)
                {
                    try
                    {
                        UltimoProgressivo = item.progressivo;

                        string[] splitDestinazione = item.destinazione.Split('|');
                        string[] splitDestinazioneBolla = string.IsNullOrEmpty(item.destinazione_bolla) ? new string[0] : item.destinazione_bolla.Replace("$", string.Empty).Replace("{", string.Empty).Replace("}", string.Empty).Split('|');

                        string destinazioneID = item.destinazione_id == 0 ? item.destinazione_idanagrafica.ToString() : string.Concat(item.destinazione_idanagrafica.ToString(), "_", item.destinazione_id.ToString());

                        string ragioneSociale = string.IsNullOrEmpty(splitDestinazione.GetValue(2).ToString()) ? splitDestinazione.GetValue(1).ToString() : string.Concat(splitDestinazione.GetValue(1).ToString(), " ", splitDestinazione.GetValue(2).ToString());

                        string destinatario = splitDestinazioneBolla.Length > 1 ?
                            string.IsNullOrEmpty(splitDestinazioneBolla.GetValue(2).ToString()) ? splitDestinazioneBolla.GetValue(1).ToString() : string.Concat(splitDestinazioneBolla.GetValue(1).ToString(), " ", splitDestinazioneBolla.GetValue(2).ToString()) :
                            string.IsNullOrEmpty(splitDestinazione.GetValue(2).ToString()) ? splitDestinazione.GetValue(1).ToString() : string.Concat(splitDestinazione.GetValue(1).ToString(), " ", splitDestinazione.GetValue(2).ToString());

                        //EventLog.WriteEntry(nameof(PaddyService), "Check Unloading [" + UltimoProgressivo + "] : PP_articolo!", EventLogEntryType.Warning);

                        //tabella PP_articolo
                        var articolo = _paddyPickingService.GetArticolo(item.articolo_id, item.maga_id);
                        if (articolo is null)
                        {
                            articolo = new PP_articolo
                            {
                                id = item.articolo_id,
                                maga_id = item.maga_id,
                                vano_id = item.vano_id,
                                descr = item.articolo_descr,
                                descrprint = item.articolo_descr,
                                cat = item.articolo_cat,
                                catorder = "",
                                umxpz = (int)item.articolo_qlotto,
                                umisu = item.articolo_um,
                                multipli = 0,
                                glotto = "N",
                                gmatr = "N",
                                note = item.articolo_note,
                                ubicazione = string.IsNullOrEmpty(item.articolo_ubicazione.Trim()) ? "nd" : item.articolo_ubicazione.Trim(),
                                cod_fornitore = string.IsNullOrEmpty(item.cod_fornitore.Trim()) ? "nd" : item.cod_fornitore.Trim()
                            };

                            _paddyPickingService.Create(articolo);
                        }
                        else
                        {
                            string ubicazione = string.IsNullOrEmpty(item.articolo_ubicazione.Trim()) ? "nd" : item.articolo_ubicazione.Trim();
                            string cod_fornitore = string.IsNullOrEmpty(item.cod_fornitore.Trim()) ? "nd" : item.cod_fornitore.Trim();

                            if (!articolo.ubicazione.Equals(ubicazione) ||
                                !articolo.cod_fornitore.Equals(cod_fornitore) ||
                                !articolo.umxpz.Equals((int)item.articolo_qlotto) ||
                                !articolo.descr.Equals(item.articolo_descr) ||
                                !articolo.descrprint.Equals(item.articolo_descr))
                            {

                                articolo.ubicazione = ubicazione;
                                articolo.cod_fornitore = cod_fornitore;
                                articolo.umxpz = (int)item.articolo_qlotto;
                                articolo.descr = item.articolo_descr;
                                articolo.descrprint = item.articolo_descr;

                                _paddyPickingService.Update(articolo);

                                EventLog.WriteEntry(nameof(PaddyService), "UPDATED: " + item.progressivo + "\nArticolo: " + item.articolo_id + "\nUbicazione: " + ubicazione + " | Cod.Fornitore: " + cod_fornitore + "\nUbicazione: " + articolo.ubicazione + " | Cod.Fornitore: " + articolo.cod_fornitore, EventLogEntryType.Warning);
                            }
                        }

                        //EventLog.WriteEntry(nameof(PaddyService), "Check Unloading [" + UltimoProgressivo + "] : PP_maga!", EventLogEntryType.Warning);

                        //tabella PP_maga
                        var maga = _paddyPickingService.GetMaga(item.maga_id, item.articolo_id, item.vano_id, item.maga_lotto);
                        if (maga is null)
                        {
                            maga = new PP_maga
                            {
                                maga_id = item.maga_id,
                                articolo_id = item.articolo_id,
                                vano_id = item.vano_id,
                                maga_lotto = item.maga_lotto,
                                giac = 0
                            };

                            _paddyPickingService.Create(maga);
                        }

                        //EventLog.WriteEntry(nameof(PaddyService), "Check Unloading [" + UltimoProgressivo + "] : PP_destinazione!", EventLogEntryType.Warning);

                        //tabella PP_destinazione
                        var destinazione = _paddyPickingService.GetDestinazione(destinazioneID);
                        if (destinazione is null)
                        {
                            destinazione = new PP_destinazione
                            {
                                id = destinazioneID,
                                tipo = splitDestinazioneBolla.Length > 1 ? "C" : splitDestinazione.GetValue(0).ToString(),
                                ragso = ragioneSociale,
                                piva = item.destinazione_piva,
                                cf = item.destinazione_cf,
                                destinatario = destinatario.ToUpper(),
                                nazione = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(8).ToString() : splitDestinazione.GetValue(3).ToString(),
                                indirizzo = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(3).ToString() : splitDestinazione.GetValue(4).ToString(),
                                citta = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(6).ToString() : splitDestinazione.GetValue(5).ToString(),
                                provincia = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(7).ToString() : splitDestinazione.GetValue(6).ToString(),
                                cap = splitDestinazioneBolla.Length > 1 ? splitDestinazioneBolla.GetValue(5).ToString() : splitDestinazione.GetValue(7).ToString(),
                                note = "",
                                area = "",
                                areaorder = 0,
                                prioritario = false
                            };

                            _paddyPickingService.Create(destinazione);
                        }

                        //EventLog.WriteEntry(nameof(PaddyService), "Check Unloading [" + UltimoProgressivo + "] : PP_magaord!", EventLogEntryType.Warning);

                        //tabella PP_magaord
                        var magaOrd = _paddyPickingService.GetMagaOrd(item.id, item.anno, item.progressivo, item.nriga);
                        if (magaOrd is null)
                        {
                            magaOrd = new PP_magaord
                            {
                                id = item.id,
                                anno = item.anno,
                                progressivo = item.progressivo,
                                nriga = (int)item.nriga,
                                articolo_id = item.articolo_id,
                                causale_id = item.causale_id,
                                doctipo_id = item.doctipo_id,
                                sezionale = item.sezionale,
                                maga_lotto = item.maga_lotto,
                                destinazione_id = destinazione is null ? destinazioneID : destinazione.id,
                                dataconsegna = item.dataconsegna,
                                qnt = (int)item.qnt,
                                lista_id = "INGRESSI",
                                vano_id = item.vano_id,
                                vettore_id = item.vettore_id,
                                pagina = item.pagina,
                                info_row = item.info_row,
                                info_full = item.info_full,
                                pausable = item.pausable == 1 ? true : false,
                                maga_id = item.maga_id,
                                movtype = item.movtype,
                                stato = item.stato,
                                ddtinforequired = item.ddtinforequired == 1 ? true : false,
                                data_creazione = DateTime.Now
                            };

                            _paddyPickingService.Create(magaOrd);

                            EventLog.WriteEntry(nameof(PaddyService), "INSERTED : " + magaOrd.progressivo + " - " + magaOrd.articolo_id + " - " + magaOrd.qnt + " - " + magaOrd.stato, EventLogEntryType.Warning);
                        }
                        else
                        {
                            //se esiste ed è in stato cancellato(5), elimino la riga sul palmarino per permettere la gestione della missione di magazzino
                            if (magaOrd.stato == 5)
                            {
                                //elimino la missione di magazzino presente a DB
                                _paddyPickingService.Delete(magaOrd);

                                EventLog.WriteEntry(nameof(PaddyService), "DELETED : " + magaOrd.progressivo + " - " + magaOrd.articolo_id + " - " + magaOrd.qnt + " - " + magaOrd.stato, EventLogEntryType.Warning);
                            }
                        }

                        //EventLog.WriteEntry(nameof(PaddyService), "Check Unloading [" + UltimoProgressivo + "] : PP_articolo_ean!", EventLogEntryType.Warning);

                        //tabella PP_articolo_ean
                        var articoloEAN = _paddyPickingService.GetArticoloEAN(item.articolo_id);
                        if (articoloEAN is null)
                        {
                            articoloEAN = new PP_articolo_ean
                            {
                                articolo_id = item.articolo_id,
                                maga_id = item.maga_id,
                                ean = item.articolo_EAN
                            };

                            _paddyPickingService.Create(articoloEAN);
                        }
                        else
                        {
                            if (!articoloEAN.ean.Equals(item.articolo_EAN))
                            {

                                articoloEAN.ean = item.articolo_EAN;

                                _paddyPickingService.Update(articoloEAN);

                                EventLog.WriteEntry(nameof(PaddyService), "Articolo: " + item.articolo_id + "\nEAN: " + item.articolo_EAN, EventLogEntryType.Warning);
                            }
                        }

                        idxRow++;
                    }
                    catch (Exception ex)
                    {
                        EventLog.WriteEntry(nameof(PaddyService), "Check Unloading [" + UltimoProgressivo + "] : " + ex.StackTrace, EventLogEntryType.Error);
                        CommonUtility.WriteLog(CommonUtility.PathLog, nameof(PaddyService) + " - Check Unloading [" + UltimoProgressivo + "] : " + ex.StackTrace);
                        //CommonUtility.InvioMail(CommonUtility.EMailResponsabileIT, CommonUtility.EMailAmministratore, nameof(PaddyService) + " - Check Unloading [" + UltimoProgressivo + "]", ex.Message, string.Empty, null);
                        continue;
                    }
                }

                StringBuilder sbSQL = new StringBuilder();

                #region Aggiornamento da Paddy a ReadyPro

                //EventLog.WriteEntry(nameof(PaddyService), "Check Unloading [" + UltimoProgressivo + "] : GetMagaMov!", EventLogEntryType.Warning);

                //leggo i movimenti di magazzino confermati
                //var listReadyPro = _paddyPickingService.GetMagaMov(0, "OF").OrderBy(o => o.progressivo).ThenBy(o => o.magaord_nriga).ToList();
                List<PP_magamov> listReadyPro = (
                    from p in _paddyPickingService.GetMagaMov(0, "OF")
                    group p by new
                    {
                        p.id,
                        p.magaord_anno,
                        p.magaord_id,
                        p.magaord_progressivo,
                        p.magaord_nriga,
                        p.progressivo
                    } into g
                    select new PP_magamov()
                    {
                        id = g.Key.id,
                        magaord_anno = g.Key.magaord_anno,
                        magaord_id = g.Key.magaord_id,
                        magaord_progressivo = g.Key.magaord_progressivo,
                        magaord_nriga = g.Key.magaord_nriga,
                        qnt = g.Sum(x => x.qnt),
                    }).ToList()
                    .OrderBy(o => o.progressivo)
                    .ThenBy(o => o.magaord_nriga).ToList();

                sbSQL.Clear();
                foreach (PP_magamov item in listReadyPro)
                {
                    //aggiorno i dati su Ready solo se la missione di magazzina ha delle quantità confermate
                    if (item.qnt.HasValue && item.qnt.Value > 0)
                    {
                        sbSQL.AppendLine(string.Format(SQLUpdate_Bolle, 47, int.Parse(item.magaord_id)));
                        sbSQL.AppendLine(string.Format(SQLUpdate_BolleDetteglio2, item.qnt.Value, item.magaord_nriga));
                    }

                    sbSQL.AppendLine(string.Format(SQLUpdate_MagaMov, 1, item.id));
                }

                if (!string.IsNullOrEmpty(sbSQL.ToString()))
                {
                    CommonUtility.WriteLog(CommonUtility.PathLog, "SQL : " + sbSQL.ToString());
                    readyUtilita.EseguiScript(sbSQL.ToString());
                }

                //EventLog.WriteEntry(nameof(PaddyService), "Check Unloading [" + UltimoProgressivo + "] : FINE!", EventLogEntryType.Warning);

                #endregion
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(nameof(PaddyService), "Check Unloading [" + UltimoProgressivo + "] : " + ex.StackTrace, EventLogEntryType.Error);
                CommonUtility.WriteLog(CommonUtility.PathLog, nameof(PaddyService) + " - Check Unloading [" + UltimoProgressivo + "] : " + ex.StackTrace);
                //CommonUtility.InvioMail(CommonUtility.EMailResponsabileIT, null, nameof(PaddyService) + " - Check Unloading [" + UltimoProgressivo + "]", ex.Message, string.Empty, null);
            }

            //string dtFine = DateTime.Now.ToString();

            //EventLog.WriteEntry(nameof(PaddyService), "Check Unloading : Inizio [" + dtInizio + "] | Fine [" + dtFine + "]", EventLogEntryType.Information);
        }

        private string AssegnaLista(List<OrderListModel> orderList, PackServices.ReadyPro.Data.Models.PaddyPicking paddyPickingItem, IList<PackServices.ReadyPro.Data.Models.PaddyPicking> paddyPickingList, OrderUserModel user, string destinazioneID)
        {
            string ListaID = paddyPickingList.Where(p => p.id == paddyPickingItem.id && p.anno == paddyPickingItem.anno && p.progressivo == paddyPickingItem.progressivo && !string.IsNullOrEmpty(p.lista_paddy)).Select(p => p.lista_paddy).FirstOrDefault();

            var orderExist = orderList.Find(o => o.OrderID == paddyPickingItem.id);

            if (!string.IsNullOrEmpty(paddyPickingItem.lista_paddy))
                ListaID = paddyPickingItem.lista_paddy;
            else if (paddyPickingItem.gruppo_id.Equals(28)) //Stampato Magazzino (MAUSER)
                ListaID = "MAUSER";
            else if (paddyPickingItem.gruppo_id.Equals(29)) //Stampato Magazzino (JCOPLASTIC)
                ListaID = "JCOPLASTIC";
            else if (paddyPickingItem.gruppo_id.Equals(30)) //Stampato Magazzino (SEMBOL)
                ListaID = "SEMBOL";
            else if (paddyPickingItem.gruppo_id.Equals(31)) //Stampato Magazzino (CASONE)
                ListaID = "CASONE";
            else if (paddyPickingItem.gruppo_id.Equals(32)) //Stampato Magazzino (I.C.S.)
                ListaID = "I.C.S.";
            else if (paddyPickingItem.gruppo_id.Equals(33)) //Stampato Magazzino (NERI)
                ListaID = "NERI";
            else if (paddyPickingItem.gruppo_id.Equals(34)) //Stampato Magazzino (KARTELL)
                ListaID = "KARTELL";
            else if (paddyPickingItem.gruppo_id.Equals(35)) //Stampato Magazzino (MOBIL PLASTIC)
                ListaID = "MOBIL PLASTIC";
            else if (paddyPickingItem.gruppo_id.Equals(36)) //Stampato Magazzino (P.P.E.)
                ListaID = "P.P.E.";
            else if (paddyPickingItem.gruppo_id.Equals(37)) //Stampato Magazzino (CARVEL)
                ListaID = "CARVEL";
            else if (paddyPickingItem.gruppo_id.Equals(38)) //Stampato Magazzino (OMCE)
                ListaID = "OMCE";
            else if (paddyPickingItem.gruppo_id.Equals(39)) //Stampato Magazzino (FASMA)
                ListaID = "FASMA";
            else if (paddyPickingItem.gruppo_id.Equals(40)) //Stampato Magazzino (Fornitori Vari)
                ListaID = "FORNITORI VARI";
            else if (paddyPickingItem.gruppo_id.Equals(41)) //Stampato Magazzino (RIMOL)
                ListaID = "RIMOL";
            else if (paddyPickingItem.gruppo_id.Equals(42)) //Stampato Magazzino (BERICAH)
                ListaID = "BERICAH";
            else if (paddyPickingItem.gruppo_id.Equals(43)) //Stampato Magazzino (SALL)
                ListaID = "SALL";
            else if (paddyPickingItem.vettore_id == "RITIRA IL CLIENTE")
                ListaID = "RITIRA IL CLIENTE";
            else if (paddyPickingItem.urgente)
                ListaID = "URGENTE";
            else if (destinazioneID == "20521" || destinazioneID.StartsWith("20521_"))
                ListaID = "UNCLE BETO";
            else if (paddyPickingItem.pagamento_id.Equals(70))
                ListaID = "ManoMano";
            else
            {
                if (orderExist is null)
                {
                    if (string.IsNullOrEmpty(ListaID))
                    {
                        //ListaID = paddyPickingItem.lista_id;
                        ListaID = user.ListaID;
                        user.OrderTotal += 1;
                    }
                }
                else
                    ListaID = orderExist.LabelList;
            }

            return ListaID;
        }

        protected void UpdateEAN()
        {
            string dtInizio = DateTime.Now.ToString();

            IList<PP_articolo_ean> updateEAN = null;

            try
            {
                updateEAN = _paddyPickingService.UpdateEAN_Massive();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(nameof(PaddyService), "Udpate EAN : " + ex.StackTrace, EventLogEntryType.Error);
                CommonUtility.WriteLog(CommonUtility.PathLog, nameof(PaddyService) + " - Udpate EAN : " + ex.StackTrace);
                CommonUtility.InvioMail(CommonUtility.EMailResponsabileIT, null, nameof(PaddyService) + " - Udpate EAN", ex.Message, string.Empty, null);
            }

            string dtFine = DateTime.Now.ToString();

            int rowsUpdate = updateEAN is null ? 0 : updateEAN.Count();
            string printMessage = string.Format("Udpate EAN - aggiornati {0} EAN : Inizio [{1}] | Fine [{2}]", rowsUpdate.ToString(), dtInizio, dtFine);

            EventLog.WriteEntry(nameof(PaddyService), printMessage, EventLogEntryType.Information);
        }
    }

    public class OrderUserModel
    {
        public string ListaID { get; set; }
        public int OrderTotal { get; set; }
    }

    public class OrderListModel
    {
        public int OrderID { get; set; }
        public string LabelList { get; set; }

        public OrderListModel(int _orderID, string _labelList)
        {
            OrderID = _orderID;
            LabelList = _labelList;
        }
    }
}