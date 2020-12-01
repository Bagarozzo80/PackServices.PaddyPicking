namespace PaddyPicking
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Variabile di progettazione necessaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Pulire le risorse in uso.
        /// </summary>
        /// <param name="disposing">ha valore true se le risorse gestite devono essere eliminate, false in caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Codice generato da Progettazione componenti

        /// <summary>
        /// Metodo necessario per il supporto della finestra di progettazione. Non modificare
        /// il contenuto del metodo con l'editor di codice.
        /// </summary>
        private void InitializeComponent()
        {
            this.PaddyPickingServiceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.PaddyPickingServiceInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // PaddyPickingServiceProcessInstaller
            // 
            this.PaddyPickingServiceProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.PaddyPickingServiceProcessInstaller.Password = null;
            this.PaddyPickingServiceProcessInstaller.Username = null;
            // 
            // PaddyPickingServiceInstaller
            // 
            this.PaddyPickingServiceInstaller.Description = "Paddy Picking - Servizio di sync automatico da e verso Ready";
            this.PaddyPickingServiceInstaller.DisplayName = "Paddy Picking - Sync";
            this.PaddyPickingServiceInstaller.ServiceName = "PaddyService";
            this.PaddyPickingServiceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.PaddyPickingServiceInstaller,
            this.PaddyPickingServiceProcessInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller PaddyPickingServiceProcessInstaller;
        private System.ServiceProcess.ServiceInstaller PaddyPickingServiceInstaller;
    }
}