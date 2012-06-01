using System;
using System.Drawing;
using System.Windows.Forms;
using Localization;
using SayMore.Media.Audio;
using SayMore.Model;
using SayMore.Properties;
using SayMore.UI.SessionRecording;
using SayMore.UI.NewSessionsFromFiles;
using SayMore.UI.ProjectWindow;

namespace SayMore.UI.ElementListScreen
{
	/// ----------------------------------------------------------------------------------------
	public partial class SessionsListScreen : ConcreteSessionScreen, ISayMoreView
	{
		private readonly NewSessionsFromFileDlgViewModel.Factory _newSessionsFromFileDlgViewModel;

		/// ------------------------------------------------------------------------------------
		public SessionsListScreen(ElementListViewModel<Session> presentationModel,
			NewSessionsFromFileDlgViewModel.Factory newSessionsFromFileDlgViewModel,
			SessionsGrid.Factory sessionGridFactory)
			: base(presentationModel)
		{
			_elementsGrid = sessionGridFactory();
			_elementsGrid.Name = "SessionsGrid";
			_newSessionsFromFileDlgViewModel = newSessionsFromFileDlgViewModel;
			InitializeComponent();

			if (DesignMode)
				return;

			Initialize(_componentsSplitter.Panel2, _sessionComponentFileGrid, _sessionsListPanel);
			_sessionComponentFileGrid.InitializeGrid("SessionScreen",
				LocalizationManager.GetString("SessionsView.FileList.AddSessionsButtonToolTip", "Add Files to the Session"));

			_elementsListPanel.InsertButton(1, _buttonNewFromFiles);
			_elementsListPanel.InsertButton(2, _buttonNewFromRecording);

			InitializeMenus();

			if (_componentsSplitter.Panel2.Controls.Count > 1)
				_labelClickNewHelpPrompt.Visible = false;
			else
				_componentsSplitter.Panel2.ControlAdded += HandleFirstSetOfComponentEditorsAdded;

			_componentsSplitter.Panel2.ControlRemoved += HandleLastSetOfComponentEditorsRemoved;

			_elementsListPanel.ButtonPanelBackColor1 = Settings.Default.SessionEditorsButtonBackgroundColor1;
			_elementsListPanel.ButtonPanelBackColor2 = Settings.Default.SessionEditorsButtonBackgroundColor2;
			_elementsListPanel.ButtonPanelTopBorderColor = Settings.Default.SessionEditorsBorderColor;

			_elementsListPanel.HeaderPanelBackColor1 = Settings.Default.SessionEditorsButtonBackgroundColor2;
			_elementsListPanel.HeaderPanelBackColor2 = Settings.Default.SessionEditorsButtonBackgroundColor1;
			_elementsListPanel.HeaderPanelBottomBorderColor = Settings.Default.SessionEditorsBorderColor;
		}

		/// ------------------------------------------------------------------------------------
		private void InitializeMenus()
		{
			MainMenuItem = new ToolStripMenuItem();
			MainMenuItem.Text = LocalizationManager.GetString(
				"SessionsView.SessionsMainMenu.TopLevelMenuText", "&Session", null, MainMenuItem);

			var menu = new ToolStripMenuItem();
			MainMenuItem.DropDownItems.Add(menu);
			menu.Click += HandleAddingNewElement;
			menu.Text = LocalizationManager.GetString(
				"SessionsView.SessionsMainMenu.NewMenuText", "&New", null, menu);

			menu = new ToolStripMenuItem();
			MainMenuItem.DropDownItems.Add(menu);
			menu.Click += HandleButtonNewFromFilesClick;
			menu.Text = LocalizationManager.GetString(
				"SessionsView.SessionsMainMenu.NewFromDeviceMenuText", "New From De&vice...", null, menu);

			menu = new ToolStripMenuItem();
			MainMenuItem.DropDownItems.Add(menu);
			menu.Click += HandleButtonNewFromRecordingsClick;
			menu.Text = LocalizationManager.GetString(
				"SessionsView.SessionsMainMenu.NewFromRecordingMenuText", "New From &Recording...", null, menu);

			MainMenuItem.DropDownItems.Add(new ToolStripSeparator());

			foreach (var sessionMenuItem in _elementsGrid.GetMenuCommands())
				MainMenuItem.DropDownItems.Add(sessionMenuItem);
		}

		/// ------------------------------------------------------------------------------------
		void HandleFirstSetOfComponentEditorsAdded(object sender, ControlEventArgs e)
		{
			_componentsSplitter.Panel2.ControlAdded -= HandleFirstSetOfComponentEditorsAdded;
			_labelClickNewHelpPrompt.Visible = false;
		}

		/// ------------------------------------------------------------------------------------
		void HandleLastSetOfComponentEditorsRemoved(object sender, ControlEventArgs e)
		{
			if (_componentsSplitter.Panel2.Controls.Count == 1)
			{
				_labelClickNewHelpPrompt.Visible = true;
				_componentsSplitter.Panel2.ControlAdded += HandleFirstSetOfComponentEditorsAdded;
			}
		}

		/// ------------------------------------------------------------------------------------
		protected override void HandleComponentFileSaved(object sender, EventArgs e)
		{
			base.HandleComponentFileSaved(sender, e);
			_elementsGrid.RefreshCurrentRow();
		}

		/// ------------------------------------------------------------------------------------
		public void AddTabToTabGroup(ViewTabGroup viewTabGroup)
		{
			var tab = viewTabGroup.AddTab(this);
			tab.Name = "SessionsViewTab"; // for tests
			tab.Text = LocalizationManager.GetString("SessionsView.ViewTabText", "Sessions", null, "Sessions View", null, tab);
			Text = tab.Text;
		}

		/// ------------------------------------------------------------------------------------
		public override void ViewActivated(bool firstTime)
		{
			base.ViewActivated(firstTime);

			if (firstTime)
			{
				if (Settings.Default.SessionScreenElementsListSplitterPos > 0)
					_elementListSplitter.SplitterDistance = Settings.Default.SessionScreenElementsListSplitterPos;

				if (Settings.Default.SessionScreenComponentsSplitterPos > 0)
					_componentsSplitter.SplitterDistance = Settings.Default.SessionScreenComponentsSplitterPos;
			}
		}

		/// ------------------------------------------------------------------------------------
		public Image Image
		{
			get { return Resources.Sessions; }
		}

		/// ------------------------------------------------------------------------------------
		public ToolStripMenuItem MainMenuItem { get; private set; }

		/// ------------------------------------------------------------------------------------
		protected override Color ComponentEditorBackgroundColor
		{
			get { return Settings.Default.SessionEditorsBackgroundColor; }
		}

		/// ------------------------------------------------------------------------------------
		protected override Color ComponentEditorBorderColor
		{
			get { return Settings.Default.SessionEditorsBorderColor; }
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnHandleDestroyed(EventArgs e)
		{
			Settings.Default.SessionScreenElementsListSplitterPos = _elementListSplitter.SplitterDistance;
			Settings.Default.SessionScreenComponentsSplitterPos = _componentsSplitter.SplitterDistance;
			base.OnHandleDestroyed(e);
		}

		/// ------------------------------------------------------------------------------------
		private void HandleButtonNewFromFilesClick(object sender, EventArgs e)
		{
			using (var viewModel = _newSessionsFromFileDlgViewModel(_model))
			using (var dlg = new NewSessionsFromFilesDlg(viewModel))
			{
				viewModel.Dialog = dlg;

				if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
					LoadElementList(viewModel.FirstNewSessionAdded);

				FindForm().Focus();
			}
		}

		/// ------------------------------------------------------------------------------------
		private void HandleButtonNewFromRecordingsClick(object sender, EventArgs e)
		{
			if (!AudioUtils.GetCanRecordAudio())
				return;

			using (var viewModel = new SessionRecorderDlgViewModel())
			using (var dlg = new SessionRecorderDlg(viewModel))
			{
				if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
					return;

				var newSession = _model.CreateNewElement();
				viewModel.MoveRecordingToSessionFolder(newSession);
				LoadElementList(newSession);
				FindForm().Focus();
			}
		}
	}

	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// This class is used to overcome a limitation in the VS 2008 designer:
	/// not only can it not design a generic class, but it cannot even design a class which
	/// directly inhertis from a generic class! So we have this intermediate class.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class ConcreteSessionScreen : ElementListScreen<Session>
	{
		//design time only
		private ConcreteSessionScreen()
			: base(null)
		{}

		public ConcreteSessionScreen(ElementListViewModel<Session> presentationModel)
			: base(presentationModel)
		{}
	}
}