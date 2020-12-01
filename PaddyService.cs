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
        private readonly IDocumentService _documentService;

        //private System.Timers.Timer timerAllineaEntita;
        //private System.Timers.Timer timerAggiornaReadyPro;
        private System.Timers.Timer timerCheckEntity;

        Utilita readyUtilita = new Utilita();

        private Thread _thread;

        public PaddyService(IPaddyPickingService paddyPickingService, IDocumentService documentService)
        {
            InitializeComponent();

            _paddyPickingService = paddyPickingService;
            _documentService = documentService;

            if (!EventLog.SourceExists(nameof(PaddyService)))
                EventLog.CreateEventSource(nameof(PaddyService), "Application");
        }

        protected override void OnStart(string[] args)
        {
            timerCheckEntity = new System.Timers.Timer();
            timerCheckEntity.Interval = 60000;
            timerCheckEntity.Elapsed += new ElapsedEventHandler(timerCheckEntity_Elapsed);
            timerCheckEntity.Enabled = true;

            //timerAllineaEntita = new System.Timers.Timer();
            //timerAllineaEntita.Interval = 60000;
            //timerAllineaEntita.Elapsed += new ElapsedEventHandler(timerAllineaEntita_Elapsed);
            //timerAllineaEntita.Enabled = true;

            //timerAggiornaReadyPro = new System.Timers.Timer();
            //timerAggiornaReadyPro.Interval = 60000;
            //timerAggiornaReadyPro.Elapsed += new ElapsedEventHandler(timerAggiornaReadyPro_Elapsed);
            //timerAggiornaReadyPro.Enabled = true;
        }

        protected override void OnStop()
        {

        }

        protected void timerCheckEntity_Elapsed(object sender, ElapsedEventArgs e)
        {
            DateTime dtProcess = DateTime.Now;

            Control.CheckForIllegalCrossThreadCalls = false;

            _thread = new Thread(CheckEntity);
            _thread.Name = "Paddy Picking - Check Entity";
            _thread.IsBackground = true;
            _thread.Priority = ThreadPriority.Normal;
            _thread.Start();
        }

        //protected void timerAllineaEntita_Elapsed(object sender, ElapsedEventArgs e)
        //{
        //    DateTime dtProcess = DateTime.Now;

        //    Control.CheckForIllegalCrossThreadCalls = false;

        //    _thread = new Thread(CheckEntity);
        //    _thread.Name = "Paddy Picking - Allinea entità";
        //    _thread.IsBackground = true;
        //    _thread.Priority = ThreadPriority.Normal;
        //    _thread.Start();
        //}

        //protected void timerAggiornaReadyPro_Elapsed(object sender, ElapsedEventArgs e)
        //{
        //    DateTime dtProcess = DateTime.Now;

        //    Control.CheckForIllegalCrossThreadCalls = false;

        //    _thread = new Thread(AggiornaReadyPro);
        //    _thread.Name = "Paddy Picking - Aggiorna Ready Pro";
        //    _thread.IsBackground = true;
        //    _thread.Priority = ThreadPriority.Normal;
        //    _thread.Start();
        //}

        protected void CheckEntity()
        {
            string dtInizio = DateTime.Now.ToString();

            string SQLInsert_Articolo = "INSERT INTO [dbo].[PP_articolo] ([id], [maga_id], [vano_id], [descr], [descrprint], [cat], [catorder], [umxpz], [umisu], [multipli], [glotto], [gmatr], [note], [ubicazione], [cod_fornitore]) VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', {7}, '{8}', {9}, '{10}', '{11}', '{12}', '{13}', '{14}');";
            string SQLInsert_Magazzino = "INSERT INTO [dbo].[PP_maga] ([maga_id], [articolo_id], [vano_id], [maga_lotto], [giac]) VALUES ('{0}', '{1}', '{2}', '{3}', {4});";
            string SQLInsert_Destinazione = "INSERT INTO [dbo].[PP_destinazione] ([id], [tipo], [ragso], [piva], [cf], [destinatario], [nazione], [indirizzo], [citta], [provincia], [cap], [note], [area], [areaorder], [prioritario]) VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12}', {13}, {14});";
            string SQLInsert_Ordine = "INSERT INTO [dbo].[PP_magaord] ([id], [anno], [progressivo], [nriga], [articolo_id], [causale_id], [doctipo_id], [sezionale], [maga_lotto], [destinazione_id], [dataconsegna], [qnt], [lista_id], [vano_id], [vettore_id], [pagina], [info_row], [info_full], [pausable], [maga_id], [movtype], [stato], [ddtinforequired]) VALUES ({0}, {1}, {2}, {3}, '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', {11}, '{12}', '{13}', '{14}', '{15}', '{16}', '{17}', {18}, '{19}', {20}, {21}, {22});";

            string SQLUpdateReadyPro = "UPDATE Bolle SET [Aspetto merce] = '{0}', [Causale bolla] = {1} WHERE [ID bolla] = {2};";

            StringBuilder sbSQL = new StringBuilder();

            try
            {
                //leggo gli ordini da inserire sul sistema Paddy Picking
                var list = _paddyPickingService.GetAllPaddyPicking().ToList();

                int idxRow = 1;
                foreach (PackServices.ReadyPro.Data.Models.PaddyPicking item in list)
                {
                    //EventLog.WriteEntry(nameof(PaddyService), "idxRow: " + idxRow + "; Id:" + item.id.ToString(), EventLogEntryType.Information);

                    string[] splitDestinazione = item.destinazione.Split('|');
                    string[] splitDestinazioneBolla = item.destinazione_bolla.Replace("$", string.Empty).Replace("{", string.Empty).Replace("}", string.Empty).Split('|');

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
                            ubicazione = string.IsNullOrEmpty(item.articolo_ubicazione) ? "nd" : item.articolo_ubicazione,
                            cod_fornitore = string.IsNullOrEmpty(item.cod_fornitore) ? "nd" : item.cod_fornitore
                        };

                        sbSQL.AppendLine(string.Format(SQLInsert_Articolo,
                            articolo.id.Replace("'", "''"),
                            articolo.maga_id.Replace("'", "''"),
                            articolo.vano_id.Replace("'", "''"),
                            articolo.descr.Replace("'", "''"),
                            articolo.descrprint.Replace("'", "''"),
                            articolo.cat.Replace("'", "''"),
                            articolo.catorder.Replace("'", "''"),
                            articolo.umxpz,
                            articolo.umisu.Replace("'", "''"),
                            articolo.multipli,
                            articolo.glotto.Replace("'", "''"),
                            articolo.gmatr.Replace("'", "''"),
                            articolo.note.Replace("'", "''"),
                            articolo.ubicazione.Replace("'", "''"),
                            articolo.cod_fornitore.Replace("'", "''")));

                        //_paddyPickingService.Create(articolo);
                    }
                    else
                    {
                        articolo.ubicazione = string.IsNullOrEmpty(item.articolo_ubicazione) ? "nd" : item.articolo_ubicazione;
                        articolo.cod_fornitore = string.IsNullOrEmpty(item.cod_fornitore) ? "nd" : item.cod_fornitore;

                        //_paddyPickingService.Update(articolo);
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

                        sbSQL.AppendLine(string.Format(SQLInsert_Magazzino,
                            maga.maga_id.Replace("'", "''"),
                            maga.articolo_id.Replace("'", "''"),
                            maga.vano_id.Replace("'", "''"),
                            maga.maga_lotto.Replace("'", "''"),
                            maga.giac));

                        //_paddyPickingService.Create(maga);
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
                            destinatario = destinatario,
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

                        sbSQL.AppendLine(string.Format(SQLInsert_Destinazione,
                            destinazione.id.Replace("'", "''"),
                            destinazione.tipo.Replace("'", "''"),
                            destinazione.ragso.Replace("'", "''"),
                            destinazione.piva.Replace("'", "''"),
                            destinazione.cf.Replace("'", "''"),
                            destinazione.destinatario.Replace("'", "''"),
                            destinazione.nazione.Replace("'", "''"),
                            destinazione.indirizzo.Replace("'", "''"),
                            destinazione.citta.Replace("'", "''"),
                            destinazione.provincia.Replace("'", "''"),
                            destinazione.cap.Replace("'", "''"),
                            destinazione.note.Replace("'", "''"),
                            destinazione.area.Replace("'", "''"),
                            destinazione.areaorder,
                            destinazione.prioritario.Value ? 1 : 0));

                        //_paddyPickingService.Create(destinazione);
                    }

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
                            destinazione_id = destinazione.id,
                            dataconsegna = item.dataconsegna,
                            qnt = (int)item.qnt,
                            lista_id = item.lista_id,
                            vano_id = item.vano_id,
                            vettore_id = item.vettore_id,
                            pagina = item.pagina,
                            info_row = item.info_row,
                            info_full = item.info_full,
                            pausable = item.pausable == 1 ? true : false,
                            maga_id = item.maga_id,
                            movtype = item.movtype,
                            stato = item.stato,
                            ddtinforequired = item.ddtinforequired == 1 ? true : false
                        };

                        sbSQL.AppendLine(string.Format(SQLInsert_Ordine,
                            magaOrd.id,
                            magaOrd.anno,
                            magaOrd.progressivo,
                            magaOrd.nriga,
                            magaOrd.articolo_id.Replace("'", "''"),
                            magaOrd.causale_id.Replace("'", "''"),
                            magaOrd.doctipo_id.Replace("'", "''"),
                            magaOrd.sezionale.Replace("'", "''"),
                            magaOrd.maga_lotto.Replace("'", "''"),
                            magaOrd.destinazione_id.Replace("'", "''"),
                            magaOrd.dataconsegna.Replace("'", "''"),
                            magaOrd.qnt,
                            magaOrd.lista_id.Replace("'", "''"),
                            magaOrd.vano_id.Replace("'", "''"),
                            magaOrd.vettore_id.Replace("'", "''"),
                            magaOrd.pagina.Replace("'", "''"),
                            magaOrd.info_row.Replace("'", "''"),
                            magaOrd.info_full.Replace("'", "''"),
                            magaOrd.pausable.Value ? 1 : 0,
                            magaOrd.maga_id.Replace("'", "''"),
                            magaOrd.movtype,
                            magaOrd.stato,
                            magaOrd.ddtinforequired.Value ? 1 : 0));

                        //_paddyPickingService.Create(magaOrd);
                    }

                    idxRow++;
                }

                if (!string.IsNullOrEmpty(sbSQL.ToString()))
                    readyUtilita.EseguiScript(sbSQL.ToString());

                //pulisco lo script
                sbSQL.Clear();

                //leggo i movimenti di magazzino confermati
                var listReadyPro = _paddyPickingService.GetAllMagaMov().OrderBy(o => o.progressivo).ThenBy(o => o.magaord_nriga).ToList();

                foreach (PP_magamov item in listReadyPro)
                    sbSQL.AppendLine(string.Format(SQLUpdateReadyPro, item.ingombro_merce, 60013, int.Parse(item.magaord_id)));

                //if (!string.IsNullOrEmpty(sbSQL.ToString()))
                //readyUtilita.EseguiScript(sbSQL.ToString());
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(nameof(PaddyService), "Check Entity : " + ex.Message, EventLogEntryType.Error);
                CommonUtility.WriteLog(CommonUtility.PathLog, nameof(PaddyService) + " - Check Entity : " + ex.Message);
                CommonUtility.InvioMail(CommonUtility.EMailResponsabileIT, null, nameof(PaddyService) + " - Check Entity", ex.Message, string.Empty, null);
            }

            string dtFine = DateTime.Now.ToString();

            EventLog.WriteEntry(nameof(PaddyService), "Check Entity : Inizio [" + dtInizio + "] | Fine [" + dtFine + "]", EventLogEntryType.Information);
        }

        //protected void AggiornaReadyPro()
        //{
        //    string dtInizio = DateTime.Now.ToString();

        //    StringBuilder sbSQL = new StringBuilder();

        //    string SQLUpdate = "UPDATE Bolle SET [Aspetto merce] = '{0}', [Causale bolla] = {1} WHERE [ID bolla] = {2};";

        //    try
        //    {
        //        var list = _paddyPickingService.GetAllMagaMov().OrderBy(o => o.progressivo).ThenBy(o => o.magaord_nriga).ToList();

        //        foreach (PP_magamov item in list)
        //        {
        //            //var bolla = _documentService.GetBolle(int.Parse(item.magaord_id));

        //            //if (string.IsNullOrEmpty(bolla.Aspetto_merce))
        //            //{
        //            //bolla.Aspetto_merce = list.Find(m => m.magaord_id == item.magaord_id).ingombro_merce;

        //            //_documentService.Update(bolla);
        //            //}

        //            //readyUtilita.AggiornaCausale(int.Parse(item.magaord_id), 60013);

        //            sbSQL.AppendLine(string.Format(SQLUpdate, item.ingombro_merce, 60013, int.Parse(item.magaord_id)));

        //            //EventLog.WriteEntry(nameof(PaddyService), "Aggiorna Ready Pro : ID=" + item.magaord_id + ";Aspetto merce=" + bolla.Aspetto_merce, EventLogEntryType.Information);
        //        }

        //        EventLog.WriteEntry(nameof(PaddyService), "Aggiorna Ready Pro : " + sbSQL.ToString(), EventLogEntryType.Information);
        //        //EventLog.WriteEntry(nameof(PaddyService), "Aggiorna Ready Pro : " + list.Count.ToString(), EventLogEntryType.Information);
        //    }
        //    catch (Exception ex)
        //    {
        //        EventLog.WriteEntry(nameof(PaddyService), "Aggiorna Ready Pro : " + ex.Message, EventLogEntryType.Error);
        //        CommonUtility.WriteLog(CommonUtility.PathLog, nameof(PaddyService) + " - Aggiorna Ready Pro : " + ex.Message);
        //        CommonUtility.InvioMail(CommonUtility.EMailResponsabileIT, null, nameof(PaddyService) + " - Aggiorna Ready Pro", ex.Message, string.Empty, null);
        //    }

        //    string dtFine = DateTime.Now.ToString();

        //    EventLog.WriteEntry(nameof(PaddyService), "Aggiorna Ready Pro : Inizio [" + dtInizio + "] | Fine [" + dtFine + "]", EventLogEntryType.Information);
        //}
    }
}