using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using L10NSharp;
using SIL.Extensions;
using SIL.Reporting;
using SayMore.Model.Files;
using SayMore.Transcription.Model;
using SayMore.UI.Overview;
using SayMore.UI.ProjectChoosingAndCreating.NewProjectDialog;
using SayMore.Utilities;
using SIL.Archiving;
using SIL.Archiving.Generic;
using SIL.Archiving.IMDI;
using SayMore.Properties;
using ILists = SIL.Archiving.IMDI.Lists;
using SIL.Archiving.CMDI;
using CLists = SIL.Archiving.CMDI.Lists;
using Application = System.Windows.Forms.Application;

namespace SayMore.Model
{
	static class ArchivingHelper
	{
		/// ------------------------------------------------------------------------------------
		internal static void ArchiveUsingIMDI(IIMDIArchivable element)
		{
			var destFolder = Program.CurrentProject.IMDIOutputDirectory;

			// Move IMDI export folder to be under the mydocs/saymore
			if (string.IsNullOrEmpty(destFolder))
				destFolder = Path.Combine(NewProjectDlgViewModel.ParentFolderPathForNewProject, "IMDI Packages");

			// SP-813: If project was moved, the stored IMDI path may not be valid, or not accessible
			if (!CheckForAccessiblePath(destFolder))
			{
				destFolder = Path.Combine(NewProjectDlgViewModel.ParentFolderPathForNewProject, "IMDI Packages");
			}

			// now that we added a separate title field for projects, make sure it's not empty
			var title = string.IsNullOrEmpty(element.Title) ? element.Id : element.Title;

            // CMDI: Create IMDI Dialog View Model
			var model = new IMDIArchivingDlgViewModel(Application.ProductName, title, element.Id,
				element.ArchiveInfoDetails, element is Project, element.SetFilesToArchive, destFolder)
			{
				HandleNonFatalError = (exception, s) => ErrorReport.NotifyUserOfProblem(exception, s)
			};

            // CMDI: Actual work for filling in IMDI Dialog View Model fields
			element.InitializeModel(model);

			using (var dlg = new IMDIArchivingDlg(model, ApplicationContainer.kSayMoreLocalizationId,
				Program.DialogFont, Settings.Default.ArchivingDialog))
			{
                // CMDI: Display dialog and update settings
                // CMDI: ultimately gets to SIL.Archiving/IMDI/Schema/IMDI_3_0.cs and uses XmlSerializer
				dlg.ShowDialog(Program.ProjectWindow);
				Settings.Default.ArchivingDialog = dlg.FormSettings;

				// remember choice for next time
				if (model.OutputFolder != Program.CurrentProject.IMDIOutputDirectory)
				{
					Program.CurrentProject.IMDIOutputDirectory = model.OutputFolder;
					Program.CurrentProject.Save();
				}
			}
		}

        /// ------------------------------------------------------------------------------------
		internal static void ArchiveUsingCMDI(ICMDIArchivable element)
        {
            var destFolder = Program.CurrentProject.CMDIOutputDirectory;

            // Move CMDI export folder to be under the mydocs/saymore
            if (string.IsNullOrEmpty(destFolder))
                destFolder = Path.Combine(NewProjectDlgViewModel.ParentFolderPathForNewProject, "CMDI Packages");

            // SP-813: If project was moved, the stored CMDI path may not be valid, or not accessible
            if (!CheckForAccessiblePath(destFolder))
            {
                destFolder = Path.Combine(NewProjectDlgViewModel.ParentFolderPathForNewProject, "CMDI Packages");
            }

            // now that we added a separate title field for projects, make sure it's not empty
            var title = string.IsNullOrEmpty(element.Title) ? element.Id : element.Title;

            // CMDI: Create CMDI Dialog View Model
            //var model = new CMDIArchivingDlgViewModel(Application.ProductName, title, element.Id,
            var model = new CMDIArchivingDlgViewModel(Application.ProductName, title, element.Id,
                element.ArchiveInfoDetails, element is Project, element.SetFilesToArchive, destFolder)
            {
                HandleNonFatalError = (exception, s) => ErrorReport.NotifyUserOfProblem(exception, s)
            };

            // CMDI: Actual work for filling in CMDI Dialog View Model fields
            element.InitializeModel(model);

            //using (var dlg = new CMDIArchivingDlg(model, ApplicationContainer.kSayMoreLocalizationId,
            using (var dlg = new CMDIArchivingDlg(model, ApplicationContainer.kSayMoreLocalizationId,
                Program.DialogFont, Settings.Default.ArchivingDialog))
            {
                // CMDI: Display dialog and update settings
                dlg.ShowDialog(Program.ProjectWindow);
                Settings.Default.ArchivingDialog = dlg.FormSettings;

                // remember choice for next time
                if (model.OutputFolder != Program.CurrentProject.CMDIOutputDirectory)
                {
                    Program.CurrentProject.CMDIOutputDirectory = model.OutputFolder;
                    Program.CurrentProject.Save();
                }
            }
        }

        /// <remarks>SP-813: If project was moved, the stored IMDI path may not be valid, or not accessible</remarks>
        static internal bool CheckForAccessiblePath(string directory)
		{
			try
			{
				if (!Directory.Exists(directory))
					Directory.CreateDirectory(directory);

				var file = Path.Combine(directory, "Export.imdi");

				if (File.Exists(file)) File.Delete(file);

				File.WriteAllText(file, @"Export.imdi");

				if (File.Exists(file)) File.Delete(file);
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message);
				return false;
			}

			return true;
		}

		/// ------------------------------------------------------------------------------------
		static internal bool IncludeFileInArchive(string path, Type typeOfArchive, string metadataFileExtension)
		{
			if (path == null) return false;
			var ext = Path.GetExtension(path).ToLower();
			bool imdi = typeof(IMDIArchivingDlgViewModel).IsAssignableFrom(typeOfArchive);
			return (ext != ".pfsx" && (!imdi || (ext != metadataFileExtension)));
		}

		/// ------------------------------------------------------------------------------------
		static internal bool FileCopySpecialHandler(ArchivingDlgViewModel model, string source, string dest)
		{
			if (!source.EndsWith(AnnotationFileHelper.kAnnotationsEafFileSuffix))
				return false;

			// Fix EAF file to refer to modified name.
			AnnotationFileHelper annotationFileHelper = AnnotationFileHelper.Load(source);

			var mediaFileName = annotationFileHelper.MediaFileName;
			if (mediaFileName != null)
			{
				var normalizedName = model.NormalizeFilename(string.Empty, mediaFileName);
				if (normalizedName != mediaFileName)
				{
					annotationFileHelper.SetMediaFile(normalizedName);
					annotationFileHelper.Root.Save(dest);
					return true;
				}
			}
			return false;
		}

		internal static void SetIMDIMetadataToArchive(IIMDIArchivable element, ArchivingDlgViewModel model)
		{
			var project = element as Project;
			if (project != null)
			{
                // CMDI: General Project Data
				AddIMDIProjectData(project, model);

                // CMDI: Session data
				foreach (var session in project.GetAllSessions())
					AddIMDISession(session, model);
			}
			else
			{
				AddIMDISession((Session)element, model);
			}
		}

        internal static void SetCMDIMetadataToArchive(ICMDIArchivable element, ArchivingDlgViewModel model)
        {
            var project = element as Project;
            if (project != null)
            {
                // CMDI: General Project Data
                AddCMDIProjectData(project, model);

                // CMDI: Session data
                foreach (var session in project.GetAllSessions())
                    AddCMDISession(session, model);
            }
            else
            {
                AddCMDISession((Session)element, model);
            }
        }

        // CMDI: important
        private static void AddIMDISession(Session saymoreSession, ArchivingDlgViewModel model)
		{
			var sessionFile = saymoreSession.MetaDataFile;

			// create IMDI session
			var imdiSession = model.AddSession(saymoreSession.Id);
			imdiSession.Title = saymoreSession.Title;

			// session location
			var address = saymoreSession.MetaDataFile.GetStringValue("additional_Location_Address", null);
			var region = saymoreSession.MetaDataFile.GetStringValue("additional_Location_Region", null);
			var country = saymoreSession.MetaDataFile.GetStringValue("additional_Location_Country", null);
			var continent = saymoreSession.MetaDataFile.GetStringValue("additional_Location_Continent", null);
			if (string.IsNullOrEmpty(address))
				address = saymoreSession.MetaDataFile.GetStringValue("location", null);

			imdiSession.Location = new ArchivingLocation { Address = address, Region = region, Country = country, Continent = continent };

			// session description (synopsis)
			var stringVal = saymoreSession.MetaDataFile.GetStringValue("synopsis", null);
			if (!string.IsNullOrEmpty(stringVal))
				imdiSession.AddDescription(new LanguageString { Value = stringVal });

			// session date
			stringVal = saymoreSession.MetaDataFile.GetStringValue("date", null);
			if (!string.IsNullOrEmpty(stringVal))
				imdiSession.SetDate(DateTime.Parse(stringVal).ToISO8601TimeFormatDateOnlyString());

			// session situation
			stringVal = saymoreSession.MetaDataFile.GetStringValue("situation", null);
			if (!string.IsNullOrEmpty(stringVal))
				imdiSession.AddKeyValuePair("Situation", stringVal);

			imdiSession.Genre = GetFieldValue(sessionFile, "genre");
			imdiSession.SubGenre = GetFieldValue(sessionFile, "additional_Sub-Genre");
			imdiSession.AccessCode = GetFieldValue(sessionFile, "access");
			imdiSession.Interactivity = GetFieldValue(sessionFile, "additional_Interactivity");
			imdiSession.Involvement = GetFieldValue(sessionFile, "additional_Involvement");
			imdiSession.PlanningType = GetFieldValue(sessionFile, "additional_Planning_Type");
			imdiSession.SocialContext = GetFieldValue(sessionFile, "additional_Social_Context");
			imdiSession.Task = GetFieldValue(sessionFile, "additional_Task");

			// custom session fields
			foreach (var item in saymoreSession.MetaDataFile.GetCustomFields())
				imdiSession.AddKeyValuePair(item.FieldId, item.ValueAsString);

			// actors
			var actors = new ArchivingActorCollection();
			var persons = saymoreSession.GetAllPersonsInSession();
			foreach (var person in persons)
			{

				// is this person protected
				var protect = bool.Parse(person.MetaDataFile.GetStringValue("privacyProtection", "false"));

				// display message if the birth year is not valid
				var birthYear = person.MetaDataFile.GetStringValue("birthYear", string.Empty).Trim();
				if (!birthYear.IsValidBirthYear())
				{
					var msg = LocalizationManager.GetString("DialogBoxes.ArchivingDlg.InvalidBirthYearMsg",
						"The Birth Year for {0} should be either blank or a 4 digit number.");
					model.AdditionalMessages[string.Format(msg, person.Id)] = ArchivingDlgViewModel.MessageType.Warning;
				}

				ArchivingActor actor = new ArchivingActor
				{
					FullName = person.Id,
					Name = person.MetaDataFile.GetStringValue(PersonFileType.kCode, person.Id),
					BirthDate = birthYear,
					Gender = person.MetaDataFile.GetStringValue(PersonFileType.kGender, null),
					Education = person.MetaDataFile.GetStringValue(PersonFileType.kEducation, null),
					Occupation = person.MetaDataFile.GetStringValue(PersonFileType.kPrimaryOccupation, null),
					Anonymize = protect,
					Role = "Participant"
				};

				// do this to get the ISO3 codes for the languages because they are not in saymore
				var language = ILists.LanguageList.FindByEnglishName(person.MetaDataFile.GetStringValue("primaryLanguage", null));
				if (language != null)
					actor.PrimaryLanguage = new ArchivingLanguage(language.Iso3Code, language.EnglishName);

				language = ILists.LanguageList.FindByEnglishName(person.MetaDataFile.GetStringValue("mothersLanguage", null));
				if (language != null)
					actor.MotherTongueLanguage = new ArchivingLanguage(language.Iso3Code, language.EnglishName);

				// otherLanguage0 - otherLanguage3
				for (var i = 0; i < 4; i++)
				{
					language = ILists.LanguageList.FindByEnglishName(person.MetaDataFile.GetStringValue("otherLanguage" + i, null));
					if (language != null)
						actor.Iso3Languages.Add(new ArchivingLanguage(language.Iso3Code, language.EnglishName));
				}

				// custom person fields
				foreach (var item in person.MetaDataFile.GetCustomFields())
					actor.AddKeyValuePair(item.FieldId, item.ValueAsString);

				// actor files
				var actorFiles = Directory.GetFiles(person.FolderPath)
					.Where(f => IncludeFileInArchive(f, typeof(IMDIArchivingDlgViewModel), Settings.Default.PersonFileExtension));
				foreach (var file in actorFiles)
					actor.Files.Add(CreateArchivingFile(file));

				// add actor to imdi session
				actors.Add(actor);
			}

			// get contributors
			foreach (var contributor in saymoreSession.GetAllContributorsInSession())
			{
				var actr = actors.FirstOrDefault(a => a.Name == contributor.Name);
				if (actr == null)
				{
					actors.Add(contributor);
				}
				else
				{
					if (actr.Role == "Participant")
					{
						actr.Role = contributor.Role;
					}
					else
					{
						if (!actr.Role.Contains(contributor.Role))
							actr.Role += ", " + contributor.Role;
					}
				}

			}

			// add actors to imdi session
			foreach (var actr in actors)
				imdiSession.AddActor(actr);

			// session files
			var files = saymoreSession.GetSessionFilesToArchive(model.GetType());
			foreach (var file in files)
				imdiSession.AddFile(CreateArchivingFile(file));
		}

        // CMDI: important
        private static void AddCMDISession(Session saymoreSession, ArchivingDlgViewModel model)
        {
            var sessionFile = saymoreSession.MetaDataFile;

            // create CMDI session
            var cmdiSession = model.AddSession(saymoreSession.Id);
            cmdiSession.Title = saymoreSession.Title;

            // session location
            var address = saymoreSession.MetaDataFile.GetStringValue("additional_Location_Address", null);
            var region = saymoreSession.MetaDataFile.GetStringValue("additional_Location_Region", null);
            var country = saymoreSession.MetaDataFile.GetStringValue("additional_Location_Country", null);
            var continent = saymoreSession.MetaDataFile.GetStringValue("additional_Location_Continent", null);
            if (string.IsNullOrEmpty(address))
                address = saymoreSession.MetaDataFile.GetStringValue("location", null);

            cmdiSession.Location = new ArchivingLocation { Address = address, Region = region, Country = country, Continent = continent };

            // session description (synopsis)
            var stringVal = saymoreSession.MetaDataFile.GetStringValue("synopsis", null);
            if (!string.IsNullOrEmpty(stringVal))
                cmdiSession.AddDescription(new LanguageString { Value = stringVal });

            // session date
            stringVal = saymoreSession.MetaDataFile.GetStringValue("date", null);
            if (!string.IsNullOrEmpty(stringVal))
                cmdiSession.SetDate(DateTime.Parse(stringVal).ToISO8601TimeFormatDateOnlyString());

            // session situation
            stringVal = saymoreSession.MetaDataFile.GetStringValue("situation", null);
            if (!string.IsNullOrEmpty(stringVal))
                cmdiSession.AddKeyValuePair("Situation", stringVal);

            cmdiSession.Genre = GetFieldValue(sessionFile, "genre");
            cmdiSession.SubGenre = GetFieldValue(sessionFile, "additional_Sub-Genre");
            cmdiSession.AccessCode = GetFieldValue(sessionFile, "access");
            cmdiSession.Interactivity = GetFieldValue(sessionFile, "additional_Interactivity");
            cmdiSession.Involvement = GetFieldValue(sessionFile, "additional_Involvement");
            cmdiSession.PlanningType = GetFieldValue(sessionFile, "additional_Planning_Type");
            cmdiSession.SocialContext = GetFieldValue(sessionFile, "additional_Social_Context");
            cmdiSession.Task = GetFieldValue(sessionFile, "additional_Task");

            // custom session fields
            foreach (var item in saymoreSession.MetaDataFile.GetCustomFields())
                cmdiSession.AddKeyValuePair(item.FieldId, item.ValueAsString);

            // actors
            var actors = new ArchivingActorCollection();
            var persons = saymoreSession.GetAllPersonsInSession();
            foreach (var person in persons)
            {

                // is this person protected
                var protect = bool.Parse(person.MetaDataFile.GetStringValue("privacyProtection", "false"));

                // display message if the birth year is not valid
                var birthYear = person.MetaDataFile.GetStringValue("birthYear", string.Empty).Trim();
                if (!birthYear.IsValidBirthYear())
                {
                    var msg = LocalizationManager.GetString("DialogBoxes.ArchivingDlg.InvalidBirthYearMsg",
                        "The Birth Year for {0} should be either blank or a 4 digit number.");
                    model.AdditionalMessages[string.Format(msg, person.Id)] = ArchivingDlgViewModel.MessageType.Warning;
                }

                ArchivingActor actor = new ArchivingActor
                {
                    FullName = person.Id,
                    Name = person.MetaDataFile.GetStringValue(PersonFileType.kCode, person.Id),
                    BirthDate = birthYear,
                    Gender = person.MetaDataFile.GetStringValue(PersonFileType.kGender, null),
                    Education = person.MetaDataFile.GetStringValue(PersonFileType.kEducation, null),
                    Occupation = person.MetaDataFile.GetStringValue(PersonFileType.kPrimaryOccupation, null),
                    Anonymize = protect,
                    Role = "Participant"
                };

                // do this to get the ISO3 codes for the languages because they are not in saymore
                var language = CLists.LanguageList.FindByEnglishName(person.MetaDataFile.GetStringValue("primaryLanguage", null));
                if (language != null)
                    actor.PrimaryLanguage = new ArchivingLanguage(language.Iso3Code, language.EnglishName);

                language = CLists.LanguageList.FindByEnglishName(person.MetaDataFile.GetStringValue("mothersLanguage", null));
                if (language != null)
                    actor.MotherTongueLanguage = new ArchivingLanguage(language.Iso3Code, language.EnglishName);

                // otherLanguage0 - otherLanguage3
                for (var i = 0; i < 4; i++)
                {
                    language = CLists.LanguageList.FindByEnglishName(person.MetaDataFile.GetStringValue("otherLanguage" + i, null));
                    if (language != null)
                        actor.Iso3Languages.Add(new ArchivingLanguage(language.Iso3Code, language.EnglishName));
                }

                // custom person fields
                foreach (var item in person.MetaDataFile.GetCustomFields())
                    actor.AddKeyValuePair(item.FieldId, item.ValueAsString);

                // actor files
                var actorFiles = Directory.GetFiles(person.FolderPath)
                    .Where(f => IncludeFileInArchive(f, typeof(CMDIArchivingDlgViewModel), Settings.Default.PersonFileExtension));
                foreach (var file in actorFiles)
                    actor.Files.Add(CreateArchivingFile(file));

                // add actor to cmdi session
                actors.Add(actor);
            }

            // get contributors
            foreach (var contributor in saymoreSession.GetAllContributorsInSession())
            {
                var actr = actors.FirstOrDefault(a => a.Name == contributor.Name);
                if (actr == null)
                {
                    actors.Add(contributor);
                }
                else
                {
                    if (actr.Role == "Participant")
                    {
                        actr.Role = contributor.Role;
                    }
                    else
                    {
                        if (!actr.Role.Contains(contributor.Role))
                            actr.Role += ", " + contributor.Role;
                    }
                }

            }

            // add actors to cmdi session
            foreach (var actr in actors)
                cmdiSession.AddActor(actr);

            // session files
            var files = saymoreSession.GetSessionFilesToArchive(model.GetType());
            foreach (var file in files)
                cmdiSession.AddFile(CreateArchivingFile(file));
        }

        private static string GetFieldValue(ComponentFile file, string valueName)
		{
			var stringVal = file.GetStringValue(valueName, null);
			return string.IsNullOrEmpty(stringVal) ? null : stringVal;
		}

        // CMDI: Important
		private static void AddIMDIProjectData(Project saymoreProject, ArchivingDlgViewModel model)
		{
			var package = (IMDIPackage) model.ArchivingPackage;

			// location
			package.Location = new ArchivingLocation
			{
				Address = saymoreProject.Location,
				Region = saymoreProject.Region,
				Country = saymoreProject.Country,
				Continent = saymoreProject.Continent
			};

			// description
			package.AddDescription(new LanguageString(saymoreProject.ProjectDescription, null));

			// content type
			package.ContentType = null;

			// funding project
			package.FundingProject = new ArchivingProject
			{
				Title = saymoreProject.FundingProjectTitle,
				Name = saymoreProject.FundingProjectTitle
			};

			// athor
			package.Author = saymoreProject.ContactPerson;

			// applications
			package.Applications = null;

			// access date
			package.Access.DateAvailable = saymoreProject.DateAvailable;

			// access owner
			package.Access.Owner = saymoreProject.RightsHolder;

			// publisher
			package.Publisher = saymoreProject.Depositor;

			// subject language
			if (!string.IsNullOrEmpty(saymoreProject.VernacularISO3CodeAndName))
			{
				var parts = saymoreProject.VernacularISO3CodeAndName.SplitTrimmed(':').ToArray();
				if (parts.Length == 2)
				{
					var language = ILists.LanguageList.FindByISO3Code(parts[0]);

					// SP-765:  Allow codes from Ethnologue that are not in the Arbil list
					if ((language == null) || (string.IsNullOrEmpty(language.EnglishName)))
						package.ContentIso3Languages.Add(new ArchivingLanguage(parts[0], parts[1], parts[1]));
					else
						package.ContentIso3Languages.Add(new ArchivingLanguage(language.Iso3Code, parts[1], language.EnglishName));
				}
			}

			// project description documents
			var docsPath = Path.Combine(saymoreProject.FolderPath, ProjectDescriptionDocsScreen.kFolderName);
			if (Directory.Exists(docsPath))
			{
				var files = Directory.GetFiles(docsPath, "*.*", SearchOption.TopDirectoryOnly);

				// the directory exists and contains files
				if (files.Length > 0)
					AddDocumentsSession(ProjectDescriptionDocsScreen.kArchiveSessionName, files, model);
			}

			// other project documents
			docsPath = Path.Combine(saymoreProject.FolderPath, ProjectOtherDocsScreen.kFolderName);
			if (Directory.Exists(docsPath))
			{
				var files = Directory.GetFiles(docsPath, "*.*", SearchOption.TopDirectoryOnly);

				// the directory exists and contains files
				if (files.Length > 0)
					AddDocumentsSession(ProjectOtherDocsScreen.kArchiveSessionName, files, model);
			}
		}

        // CMDI: Important
        private static void AddCMDIProjectData(Project saymoreProject, ArchivingDlgViewModel model)
        {
            var package = (CMDIPackage)model.ArchivingPackage;

            // location
            package.Location = new ArchivingLocation
            {
                Address = saymoreProject.Location,
                Region = saymoreProject.Region,
                Country = saymoreProject.Country,
                Continent = saymoreProject.Continent
            };

            // description
            package.AddDescription(new LanguageString(saymoreProject.ProjectDescription, null));

            // content type
            package.ContentType = null;

            // funding project
            package.FundingProject = new ArchivingProject
            {
                Title = saymoreProject.FundingProjectTitle,
                Name = saymoreProject.FundingProjectTitle
            };

            // athor
            package.Author = saymoreProject.ContactPerson;

            // applications
            package.Applications = null;

            // access date
            package.Access.DateAvailable = saymoreProject.DateAvailable;

            // access owner
            package.Access.Owner = saymoreProject.RightsHolder;

            // publisher
            package.Publisher = saymoreProject.Depositor;

            // subject language
            if (!string.IsNullOrEmpty(saymoreProject.VernacularISO3CodeAndName))
            {
                var parts = saymoreProject.VernacularISO3CodeAndName.SplitTrimmed(':').ToArray();
                if (parts.Length == 2)
                {
                    var language = ILists.LanguageList.FindByISO3Code(parts[0]);

                    // SP-765:  Allow codes from Ethnologue that are not in the Arbil list
                    if ((language == null) || (string.IsNullOrEmpty(language.EnglishName)))
                        package.ContentIso3Languages.Add(new ArchivingLanguage(parts[0], parts[1], parts[1]));
                    else
                        package.ContentIso3Languages.Add(new ArchivingLanguage(language.Iso3Code, parts[1], language.EnglishName));
                }
            }

            // project description documents
            var docsPath = Path.Combine(saymoreProject.FolderPath, ProjectDescriptionDocsScreen.kFolderName);
            if (Directory.Exists(docsPath))
            {
                var files = Directory.GetFiles(docsPath, "*.*", SearchOption.TopDirectoryOnly);

                // the directory exists and contains files
                if (files.Length > 0)
                    AddDocumentsSession(ProjectDescriptionDocsScreen.kArchiveSessionName, files, model);
            }

            // other project documents
            docsPath = Path.Combine(saymoreProject.FolderPath, ProjectOtherDocsScreen.kFolderName);
            if (Directory.Exists(docsPath))
            {
                var files = Directory.GetFiles(docsPath, "*.*", SearchOption.TopDirectoryOnly);

                // the directory exists and contains files
                if (files.Length > 0)
                    AddDocumentsSession(ProjectOtherDocsScreen.kArchiveSessionName, files, model);
            }
        }

        private static ArchivingFile CreateArchivingFile(string fileName)
		{
			var annotationSuffix = AnnotationFileHelper.kAnnotationsEafFileSuffix;
			var metaFileSuffix = Settings.Default.MetadataFileExtension;

			var arcFile = new ArchivingFile(fileName);

			// is this an annotation file?
			if (fileName.EndsWith(annotationSuffix))
				arcFile.DescribesAnotherFile = fileName.Substring(0, fileName.Length - annotationSuffix.Length);

			// is this a meta file?
			if (fileName.EndsWith(metaFileSuffix))
				arcFile.DescribesAnotherFile = fileName.Substring(0, fileName.Length - metaFileSuffix.Length);

			return arcFile;
		}

		private static void AddDocumentsSession(string sessionName, string[] sourceFiles, ArchivingDlgViewModel model)
		{
			// create IMDI session
			var imdiSession = model.AddSession(sessionName);
			imdiSession.Title = sessionName;

			foreach (var file in sourceFiles)
				imdiSession.AddFile(CreateArchivingFile(file));
		}
	}
}
