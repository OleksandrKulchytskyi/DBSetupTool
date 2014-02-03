using DBSetup.Common.Helpers;
using System;

namespace DBSetup
{
	internal class StringsContainer : SingletonBase<StringsContainer>
	{
		private StringsContainer()
		{
		}

		private readonly string _exitConfirmation = "The SQL Database Setup Wizard has not finished setting up the SQL Database. \n\rAre you sure you want to exit the wizard?";

		public string ExitConfirmation { get { return _exitConfirmation; } }

		private readonly string _confirmExitTitle = "Exit confirmation";

		public string ExitConfirmationTitle { get { return _confirmExitTitle; } }

		private readonly string _dbName = "Comm4";

		public string DatabaseName { get { return _dbName; } }

		private readonly string _fileHasNotBeenChoosen = "You have not chosen file for process. Please select file";

		public string FileHasNotBeenChoosen { get { return _fileHasNotBeenChoosen; } }

        private readonly string _fileIsNotExists = "The selected file does not exist.{0}Please select a valid configuration file.".FormatWith(Environment.NewLine);

		public string FileIsNotExists { get { return _fileIsNotExists; } }

		private readonly string _fieldsAreEmpty = "All fields are required to be filled to proceed.";

		public string FieldsAreEmpty { get { return _fieldsAreEmpty; } }

		private readonly string _failToBuildLisOfSqlStatemenets = "Fail to build list of sql statements";

		public string FailToBuildSqlStatementList { get { return _failToBuildLisOfSqlStatemenets; } }

		private readonly string _lisOfSqlStatemenetsSuccess = "List of SQL statements has been built successfully";

		public string LisOfSqlStatemenetsSuccess { get { return _lisOfSqlStatemenetsSuccess; } }

		private readonly string _configScriptAndUserData = "configuration script and user entered data ({0} of {1})";

		public string ConfigScriptAndUserDataMsg { get { return _configScriptAndUserData; } }

		private readonly string _sqlCurrentStep = "SQL= {0} ({1} of {2})";

		public string SqlCurrentStepMsg { get { return _sqlCurrentStep; } }

		private readonly string _dicomCurrentStep = "DICOM={0},{1}";

		public string DICOMCurrentStepMsg { get { return _dicomCurrentStep; } }

		private readonly string _maxTryCountIsExceeded = "You have exceeded the maximum error count.\n\rDo you want to continue work on error(s)?";

		public string MaxTryCountIsExceeded { get { return _maxTryCountIsExceeded; } }

		private readonly string _DbExists = "Do you want to overwrite the existing PowerScribe 360 database?";

		public string DbIsExistsMessage { get { return _DbExists; } }

		private readonly string _FailDetermineUpgradeType = "The current database version {0} \n\r does not have any defined upgrade path in the current Select Type list.";

		public string FailToDetermineUpgradeType { get { return _FailDetermineUpgradeType; } }

		private readonly string _latestVestionUpToDate = "You have the latest database version.\n\r Do you want to proceed?";

		public string LatestVestionUpToDate { get { return _latestVestionUpToDate; } }

		private readonly string _workisDone = "Finalization steps have been successfully completed.";

		public string FinalMessage { get { return _workisDone; } }
	}
}