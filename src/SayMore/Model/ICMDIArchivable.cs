using SIL.Archiving;
using SIL.Archiving.CMDI;

namespace SayMore.Model
{
	internal interface ICMDIArchivable
	{
		string ArchiveInfoDetails { get; }

		string Title { get; }

		string Id { get; }

		void InitializeModel(CMDIArchivingDlgViewModel model);

		void SetFilesToArchive(ArchivingDlgViewModel model);
	}
}
