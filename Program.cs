using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ESRI.ArcGIS.esriSystem;

namespace FissureStationImport
{
    static class Program
    {
        private static LicenseInitializer m_AOLicenseInitializer = new FissureStationImport.LicenseInitializer();
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //ESRI License Initializer generated code.
            m_AOLicenseInitializer.InitializeApplication(new esriLicenseProductCode[] { esriLicenseProductCode.esriLicenseProductCodeArcInfo },
            new esriLicenseExtensionCode[] { });
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Main());
            //ESRI License Initializer generated code.
            //Do not make any call to ArcObjects after ShutDownApplication()
            m_AOLicenseInitializer.ShutdownApplication();
        }
    }
}