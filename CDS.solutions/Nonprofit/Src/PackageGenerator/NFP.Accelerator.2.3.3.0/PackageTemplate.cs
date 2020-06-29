using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Uii.Common.Entities;
using Microsoft.Xrm.Tooling.PackageDeployment;
using Microsoft.Xrm.Tooling.PackageDeployment.CrmPackageExtentionBase;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;

namespace NFP.Accelerator._2._3._3._0
{
    /// <summary>
    /// Import package starter frame. 
    /// </summary>
    [Export(typeof(IImportExtensions))]
    public class PackageTemplate : ImportExtension

    {
        private const string ANCHOR_SOLUTION_UNIQUE_NAME = "MicrosoftDynamicsNonprofitAcceleratorCE";


        /// <summary>
        /// Override default decision made by PD.
        /// </summary>
        public override UserRequestedImportAction OverrideSolutionImportDecision(string solutionUniqueName, Version organizationVersion, Version packageSolutionVersion, Version inboundSolutionVersion, Version deployedSolutionVersion, ImportAction systemSelectedImportAction)
        {


            // Solution appears in settings if the request came from the SPA. Follow that request if it exists.
            if (RuntimeSettings != null && RuntimeSettings.ContainsKey(solutionUniqueName))
            {
                bool install = false;
                if (bool.TryParse((string)RuntimeSettings[solutionUniqueName], out install) && !install)
                {
                    PackageLog.Log("Skipping package: " + solutionUniqueName);
                    return UserRequestedImportAction.Skip;
                }
            }
            // If this request didn't come from the SPA, then this is an upgrade, but default to allow the anchor solution. We don't want to install new solutions,
            // so only import the ones that already exist on the instance
            else if (solutionUniqueName?.Equals(ANCHOR_SOLUTION_UNIQUE_NAME) == false && deployedSolutionVersion.Equals(new Version(0, 0, 0, 0)))
            {
                return UserRequestedImportAction.Skip;
            }

            PackageLog.Log("Not skipping package: " + solutionUniqueName);
            return base.OverrideSolutionImportDecision(solutionUniqueName, organizationVersion, packageSolutionVersion, inboundSolutionVersion, deployedSolutionVersion, systemSelectedImportAction);
        }

        /// <summary>
        /// Returns the version of specified solution if present.
        /// </summary>
        private int[] GetSolutionVersion(string solutionName)
        {
            FilterExpression filter = new FilterExpression(LogicalOperator.And);
            filter.AddCondition(new ConditionExpression("uniquename", ConditionOperator.Equal, solutionName));
            QueryExpression query = new QueryExpression
            {
                EntityName = "solution",
                ColumnSet = new ColumnSet("uniquename", "version"),
                Criteria = filter,
                PageInfo = new PagingInfo() { ReturnTotalRecordCount = true }
            };
            EntityCollection stuff = CrmSvc.RetrieveMultiple(query);
            foreach (var i in stuff.Entities)
            {
                PackageLog.Log($"Name: {i.GetAttributeValue<string>("uniquename").ToString()} Version: {i.GetAttributeValue<string>("version")}");
            }
            if (stuff.TotalRecordCount < 1) return null;
            string version = stuff.Entities.First().GetAttributeValue<string>("version");
            if (string.IsNullOrEmpty(version)) return null;
            string[] versions = version.Split('.');

            PackageLog.Log($"NonprofitCore - Major:{versions[0]} Minor:{versions[1]} Build:{versions[2]} Revision:{versions[3]}");
            int[] versionNumbers = new int[4];
            int.TryParse(versions[0], out versionNumbers[0]);
            int.TryParse(versions[1], out versionNumbers[1]);
            int.TryParse(versions[2], out versionNumbers[2]);
            int.TryParse(versions[3], out versionNumbers[3]);

            return versionNumbers;
        }
        /// <summary>
        /// Checks if a selected runtime setting is true or falss.
        /// </summary>
        private bool? GetRuntimeSetting(string runtimeKey)
        {
            if (RuntimeSettings != null && RuntimeSettings.ContainsKey(runtimeKey))
            {
                bool install = false;
                if (bool.TryParse((string)RuntimeSettings[runtimeKey], out install))
                {
                    return install;
                }
                else return null;
            }
            else return null;
        }
        /// <summary>
        /// Called When the package is initialized. 
        /// </summary>
        public override void InitializeCustomExtension()
        {
            if (RuntimeSettings != null)
            {
                //RuntimeSettings.Add("CommonDataModelNonprofitSampleAppsBase", "true");
                PackageLog.Log(string.Format("Runtime Settings populated.  Count = {0}", RuntimeSettings.Count));
                                
                foreach (var setting in RuntimeSettings)
                {
                    PackageLog.Log(string.Format("Key={0} | Value={1}", setting.Key, setting.Value.ToString()));
                }

                int[] version = GetSolutionVersion("NonprofitCore");
                bool? installCore = GetRuntimeSetting("NonprofitCore");
                if (installCore == true && version != null && version[0] < 3 && version[1] < 3)
                {
                    PackageLog.Log("Older version of the Core found adding sample app base solution.");
                    RuntimeSettings.Add("CommonDataModelNonprofitSampleAppsBase", "true");
                }
                else
                {
                    PackageLog.Log("New install detected no sample app base install required.");
                    RuntimeSettings.Add("CommonDataModelNonprofitSampleAppsBase", "false");
                }

                if (RuntimeSettings.ContainsKey("SkipSampleData"))
                {
                    bool sample = false;
                    if (bool.TryParse((string)RuntimeSettings["SkipSampleData"], out sample))
                    {
                        DataImportBypass = sample;
                    }
                }
            }
            else
            {
                // Skip sample data by default
                DataImportBypass = true;
            }
        }

        /// <summary>
        /// Called Before Import Completes. 
        /// </summary>
        /// <returns></returns>
        public override bool BeforeImportStage()
        {
            return true; // do nothing here. 
        }

        /// <summary>
        /// Called for each UII record imported into the system
        /// This is UII Specific and is not generally used by Package Developers
        /// </summary>
        /// <param name="app">App Record</param>
        /// <returns></returns>
        public override ApplicationRecord BeforeApplicationRecordImport(ApplicationRecord app)
        {
            return app;  // do nothing here. 
        }

        /// <summary>
        /// Called during a solution upgrade while both solutions are present in the target CRM instance. 
        /// This function can be used to provide a means to do data transformation or upgrade while a solution update is occurring. 
        /// </summary>
        /// <param name="solutionName">Name of the solution</param>
        /// <param name="oldVersion">version number of the old solution</param>
        /// <param name="newVersion">Version number of the new solution</param>
        /// <param name="oldSolutionId">Solution ID of the old solution</param>
        /// <param name="newSolutionId">Solution ID of the new solution</param>
        public override void RunSolutionUpgradeMigrationStep(string solutionName, string oldVersion, string newVersion, Guid oldSolutionId, Guid newSolutionId)
        {

            base.RunSolutionUpgradeMigrationStep(solutionName, oldVersion, newVersion, oldSolutionId, newSolutionId);
        }

        /// <summary>
        /// Called after Import completes. 
        /// </summary>
        /// <returns></returns>
        public override bool AfterPrimaryImport()
        {
            return true; // Do nothing here/ 
        }

        #region Properties

        /// <summary>
        /// Name of the Import Package to Use
        /// </summary>
        /// <param name="plural">if true, return plural version</param>
        /// <returns></returns>
        public override string GetNameOfImport(bool plural)
        {
            return "Microsoft Dynamics 365 Accelerator";
        }

        /// <summary>
        /// Folder Name for the Package data. 
        /// </summary>
        public override string GetImportPackageDataFolderName
        {
            get
            {
                // WARNING this value directly correlates to the folder name in the Solution Explorer where the ImportConfig.xml and sub content is located. 
                // Changing this name requires that you also change the correlating name in the Solution Explorer 
                return "PkgFolder";
            }
        }

        /// <summary>
        /// Description of the package, used in the package selection UI
        /// </summary>
        public override string GetImportPackageDescriptionText
        {
            get { return "Microsoft Dynamics 365 Accelerator"; }
        }

        /// <summary>
        /// Long name of the Import Package. 
        /// </summary>
        public override string GetLongNameOfImport
        {
            get { return "Microsoft Dynamics 365 Accelerator"; }
        }


        #endregion

    }
}
