using SayMore.Model.Files;

namespace SayMore.UI.ComponentEditors
{
	/// ----------------------------------------------------------------------------------------
	public partial class AudioComponentEditor : EditorBase
	{
		/// ------------------------------------------------------------------------------------
		public AudioComponentEditor(ComponentFile file)
		{
			InitializeComponent();
			Name = "Audio File Information";
			_binder.SetComponentFile(file);
		}
	}
}
