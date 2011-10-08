using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Windows.Forms;
using SayMore.Model.Files;
using SayMore.Model.Files.DataGathering;
using SayMore.Properties;
using SayMore.UI.LowLevelControls;

namespace SayMore.UI.ComponentEditors
{
	/// ----------------------------------------------------------------------------------------
	public partial class PersonBasicEditor : EditorBase
	{
		public delegate PersonBasicEditor Factory(ComponentFile file, string imageKey);

		private readonly List<ParentButton> _fatherButtons = new List<ParentButton>();
		private readonly List<ParentButton> _motherButtons = new List<ParentButton>();

		private FieldsValuesGrid _gridCustomFields;
		private FieldsValuesGridViewModel _gridViewModel;
		private readonly ImageFileType _imgFileType;

		/// ------------------------------------------------------------------------------------
		public PersonBasicEditor(ComponentFile file, string imageKey,
			AutoCompleteValueGatherer autoCompleteProvider, FieldGatherer fieldGatherer,
			ImageFileType imgFileType)
			: base(file, null, imageKey)
		{
			InitializeComponent();
			Name = "PersonEditor";

			_imgFileType = imgFileType;

			_fatherButtons.AddRange(new[] {_pbPrimaryLangFather, _pbOtherLangFather0,
				_pbOtherLangFather1, _pbOtherLangFather2, _pbOtherLangFather3 });

			_motherButtons.AddRange(new[] { _pbPrimaryLangMother, _pbOtherLangMother0,
				_pbOtherLangMother1, _pbOtherLangMother2, _pbOtherLangMother3 });

			_pbPrimaryLangFather.Tag = _primaryLanguage;
			_pbPrimaryLangMother.Tag = _primaryLanguage;
			_pbOtherLangFather0.Tag = _otherLanguage0;
			_pbOtherLangMother0.Tag = _otherLanguage0;
			_pbOtherLangFather1.Tag = _otherLanguage1;
			_pbOtherLangMother1.Tag = _otherLanguage1;
			_pbOtherLangFather2.Tag = _otherLanguage2;
			_pbOtherLangMother2.Tag = _otherLanguage2;
			_pbOtherLangFather3.Tag = _otherLanguage3;
			_pbOtherLangMother3.Tag = _otherLanguage3;

			HandleStringsLocalized();
			_binder.TranslateBoundValueBeingSaved += HandleBinderTranslateBoundValueBeingSaved;
			_binder.TranslateBoundValueBeingRetrieved += HandleBinderTranslateBoundValueBeingRetrieved;
			_binder.SetComponentFile(file);
			InitializeGrid(autoCompleteProvider, fieldGatherer);
			SetBindingHelper(_binder);
			_autoCompleteHelper.SetAutoCompleteProvider(autoCompleteProvider);

			LoadPersonsPicture();
			LoadParentLanguages();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Set values for unbound controls that need special handling.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override void OnHandleDestroyed(EventArgs e)
		{
			// Check that the person still exists.
			if (Directory.Exists(Path.GetDirectoryName(_file.PathToAnnotatedFile)))
				SaveParentLanguages();

			base.OnHandleDestroyed(e);
		}

		/// ------------------------------------------------------------------------------------
		private void InitializeGrid(IMultiListDataProvider autoCompleteProvider, FieldGatherer fieldGatherer)
		{
			_gridViewModel = new FieldsValuesGridViewModel(_file, autoCompleteProvider,
				fieldGatherer, key => _file.FileType.GetIsCustomFieldId(key));

			_gridCustomFields = new FieldsValuesGrid(_gridViewModel);
			_gridCustomFields.Dock = DockStyle.Top;
			_panelGrid.AutoSize = true;
			_panelGrid.Controls.Add(_gridCustomFields);
		}

		/// ------------------------------------------------------------------------------------
		public override void SetComponentFile(ComponentFile file)
		{
			if (_file != null && File.Exists(_file.PathToAnnotatedFile) &&
				file.PathToAnnotatedFile != _file.PathToAnnotatedFile)
			{
				SaveParentLanguages();
			}

			base.SetComponentFile(file);

			if (_gridViewModel != null)
				_gridViewModel.SetComponentFile(file);

			LoadPersonsPicture();
			LoadParentLanguages();
		}

		/// ------------------------------------------------------------------------------------
		public string PersonFolder
		{
			get { return Path.GetDirectoryName(_file.PathToAnnotatedFile); }
		}

		/// ------------------------------------------------------------------------------------
		public string PictureFileWithoutExt
		{
			get { return Path.GetFileNameWithoutExtension(_file.PathToAnnotatedFile) + "_Photo"; }
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the full path and file name for the person's picture file.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public string GetPictureFile()
		{
			var files = Directory.GetFiles(PersonFolder, PictureFileWithoutExt + ".*");
			var picFiles = files.Where(x => _imgFileType.IsMatch(x)).ToArray();
			return (picFiles.Length == 0 ? null : picFiles[0]);
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Gets the full path and file name for the meta file associated with the person's
		/// picture file (i.e. sidecar file).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public string GetPictureMetaFile()
		{
			var picFile = GetPictureFile();
			return (picFile == null ? null : _imgFileType.GetMetaFilePath(picFile));
		}

		/// ------------------------------------------------------------------------------------
		private void HandleIdEnter(object sender, EventArgs e)
		{
			// Makes sure the id's label is also visible when the id field gains focus.
			AutoScrollPosition = new Point(0, 0);
		}

		#region Methods for handling parents' language
		/// ------------------------------------------------------------------------------------
		private void LoadParentLanguages()
		{
			var fathersLanguage = _binder.GetValue("fathersLanguage");
			var pb = _fatherButtons.Find(x => ((TextBox)x.Tag).Text.Trim() == fathersLanguage);
			if (pb != null)
				pb.Selected = true;

			var mothersLanguage = _binder.GetValue("mothersLanguage");
			pb = _motherButtons.Find(x => ((TextBox)x.Tag).Text.Trim() == mothersLanguage);
			if (pb != null)
				pb.Selected = true;
		}

		/// ------------------------------------------------------------------------------------
		private void SaveParentLanguages()
		{
			var pb = _fatherButtons.SingleOrDefault(x => x.Selected);
			if (pb != null)
				_binder.SetValue("fathersLanguage", ((TextBox)pb.Tag).Text.Trim());

			pb = _motherButtons.SingleOrDefault(x => x.Selected);
			if (pb != null)
				_binder.SetValue("mothersLanguage", ((TextBox)pb.Tag).Text.Trim());
		}

		/// ------------------------------------------------------------------------------------
		private void HandleFathersLanguageChanging(object sender, CancelEventArgs e)
		{
			HandleParentLanguageChange(_fatherButtons, sender as ParentButton,
				HandleFathersLanguageChanging);
		}

		/// ------------------------------------------------------------------------------------
		private void HandleMothersLanguageChanging(object sender, CancelEventArgs e)
		{
			HandleParentLanguageChange(_motherButtons, sender as ParentButton,
				HandleMothersLanguageChanging);
		}

		/// ------------------------------------------------------------------------------------
		private static void HandleParentLanguageChange(IEnumerable<ParentButton> buttons,
			ParentButton selectedButton, CancelEventHandler changeHandler)
		{
			foreach (var pb in buttons.Where(x => x != selectedButton))
			{
				pb.SelectedChanging -= changeHandler;
				pb.Selected = false;
				pb.SelectedChanging += changeHandler;
			}
		}

		/// ------------------------------------------------------------------------------------
		private void HandleParentLanguageButtonMouseEnter(object sender, EventArgs e)
		{
			var pb = sender as ParentButton;

			if (pb.ParentType == ParentType.Father)
			{
				var tipSelected = Program.GetString("PeopleView.MetadataEditor.FatherSelectorToolTip.WhenSelected",
					"Indicates this is the father's primary language");

				var tipNotSelected = Program.GetString("PeopleView.MetadataEditor.FatherSelectorToolTip.WhenNotSelected",
					"Click to indicate this is the father's primary language");

				_tooltip.SetToolTip(pb, pb.Selected ? tipSelected : tipNotSelected);
			}
			else
			{
				var tipSelected = Program.GetString("PeopleView.MetadataEditor.MotherSelectorToolTip.WhenSelected",
					"Indicates this is the mothers's primary language");

				var tipNotSelected = Program.GetString("PeopleView.MetadataEditor.MotherSelectorToolTip.WhenNotSelected",
					"Click to indicate this is the mothers's primary language");

				_tooltip.SetToolTip(pb, pb.Selected ? tipSelected : tipNotSelected);
			}
		}

		#endregion

		#region Methods for handling the person's picture
		/// ------------------------------------------------------------------------------------
		private void HandlePersonPictureMouseClick(object sender, MouseEventArgs args)
		{
			using (var dlg = new OpenFileDialog())
			{
				var caption = Program.GetString("PeopleView.MetadataEditor.ChangePictureDlgCaption", "Change Picture");

				var imageFileTypes = Program.GetString("PeopleView.MetadataEditor.ImageFileTypes",
					"JPEG Images (*.jpg)|*.jpg|GIF Images (*.gif)|*.gif|TIFF Images (*.tif)|*.tif|PNG Images (*.png)|*.png|Bitmaps (*.bmp;*.dib)|*.bmp;*.dib|All Files (*.*)|*.*");

				dlg.Title = caption;
				dlg.CheckFileExists = true;
				dlg.CheckPathExists = true;
				dlg.Multiselect = false;
				dlg.Filter = imageFileTypes;

				if (dlg.ShowDialog(this) == DialogResult.OK)
					ChangePersonsPicture(dlg.FileName);
			}
		}

		/// ------------------------------------------------------------------------------------
		private void ChangePersonsPicture(string fileName)
		{
			Exception error = null;

			var oldPicMetaFile = GetPictureMetaFile();
			var oldPicFile = GetPictureFile();
			var newPicFile = PictureFileWithoutExt + Path.GetExtension(fileName);
			newPicFile = Path.Combine(PersonFolder, newPicFile);

			if (oldPicFile != null)
			{
				try
				{
					// Delete the old picture.
					File.Delete(oldPicFile);
				}
				catch (Exception e)
				{
					error = e;
				}
			}

			if (error == null && oldPicMetaFile != null)
			{
				try
				{
					// Rename the previous picture's meta file according to the new picture file name.
					var newPicMetaFile = _imgFileType.GetMetaFilePath(newPicFile);
					File.Move(oldPicMetaFile, newPicMetaFile);
				}
				catch (Exception e)
				{
					error = e;
				}
			}

			if (error == null)
			{
				try
				{
					// Copy the new picture file to the person's folder.
					File.Copy(fileName, newPicFile, true);
					LoadPersonsPicture();
				}
				catch (Exception e)
				{
					error = e;
				}
			}

			if (error == null)
			{
				if (ComponentFileListRefreshAction != null)
					ComponentFileListRefreshAction(null);
			}
			else
			{
				var msg = Program.GetString("PeopleView.MetadataEditor.ErrorChangingPersonsPhotoMsg",
					"There was an error changing the person's photo.");

				Palaso.Reporting.ErrorReport.NotifyUserOfProblem(error, msg);
			}
		}

		/// ------------------------------------------------------------------------------------
		private void LoadPersonsPicture()
		{
			if (_personsPicture == null)
				return;

			try
			{
				var picFile = GetPictureFile();

				if (picFile == null)
					_personsPicture.Image = Resources.kimidNoPhoto;
				else
				{
					// Do this instead of using the Load method because Load keeps a lock on the file.
					using (var fs = new FileStream(picFile, FileMode.Open, FileAccess.Read))
					{
						_personsPicture.Image = Image.FromStream(fs);
						fs.Close();
					}
				}
			}
			catch (Exception e)
			{
				var msg = Program.GetString("PeopleView.MetadataEditor.ErrorLoadingPersonsPhotoMsg",
					"There was an error loading the person's photo.");

				Palaso.Reporting.ErrorReport.NotifyUserOfProblem(e, msg);
			}
		}

		/// ------------------------------------------------------------------------------------
		private void HandlePersonPictureMouseEnterLeave(object sender, EventArgs e)
		{
			_personsPicture.Invalidate();
		}

		/// ------------------------------------------------------------------------------------
		private void HandlePersonPicturePaint(object sender, PaintEventArgs e)
		{
			if (!_personsPicture.ClientRectangle.Contains(_personsPicture.PointToClient(MousePosition)))
				return;

			var img = Resources.kimidChangePicture;
			var rc = _personsPicture.ClientRectangle;

			if (rc.Width > rc.Height)
			{
				rc.Width = rc.Height;
				rc.X = (_personsPicture.ClientRectangle.Width - rc.Width) / 2;
			}
			else if (rc.Height > rc.Width)
			{
				rc.Height = rc.Width;
				rc.Y = (_personsPicture.ClientRectangle.Height - rc.Height) / 2;
			}

			e.Graphics.DrawImage(img, rc);
		}

		/// ------------------------------------------------------------------------------------
		private void HandlePictureDragEnter(object sender, DragEventArgs e)
		{
			e.Effect = DragDropEffects.None;

			var picFile = GetPictureFileFromDragData(e.Data);
			if (picFile != null)
			{
				e.Effect = DragDropEffects.Copy;
				_personsPicture.Invalidate();
			}
		}

		/// ------------------------------------------------------------------------------------
		private void HandlePictureDragLeave(object sender, EventArgs e)
		{
			_personsPicture.Invalidate();
		}

		/// ------------------------------------------------------------------------------------
		private void HandlePictureDragDrop(object sender, DragEventArgs e)
		{
			var picFile = GetPictureFileFromDragData(e.Data);
			if (picFile != null)
				ChangePersonsPicture(picFile);
		}

		/// ------------------------------------------------------------------------------------
		private string GetPictureFileFromDragData(IDataObject data)
		{
			if (!data.GetFormats().Contains(DataFormats.FileDrop))
				return null;

			var list = data.GetData(DataFormats.FileDrop) as string[];
			if (list == null || list.Length != 1)
				return null;

			return (_imgFileType.IsMatch(list[0]) ? list[0] : null);
		}

		#endregion

		#region Methods for handling localized gender names
		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Update the tab text and gender names in case they were localized.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override void HandleStringsLocalized()
		{
			TabText = Program.GetString("PeopleView.MetadataEditor.TabText", "Person");

			if (_gender != null)
			{
				int i = _gender.SelectedIndex;
				_gender.Items.Clear();
				_gender.Items.Add(Program.GetString("PeopleView.MetadataEditor.GenderSelector.Male", "Male"));
				_gender.Items.Add(Program.GetString("PeopleView.MetadataEditor.GenderSelector.Female", "Female"));
				_gender.SelectedIndex = i;
			}

			base.HandleStringsLocalized();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Instead of letting the binding helper set the gender combo box value from the
		/// value in the file (which will be the English text for male or female), we'll
		/// intercept the process since the text in the gender combo box may have been
		/// localized to non English text.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private bool HandleBinderTranslateBoundValueBeingRetrieved(BindingHelper helper,
			Control boundControl, string valueFromFile, out string translatedValue)
		{
			translatedValue = null;

			if (boundControl != _gender)
				return false;

			_gender.SelectedIndex = (valueFromFile == "Male" ? 0 : 1);
			return true;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// When the binding helper gets to writing field values to the metadata file, we need
		/// to make sure the English values for male and female are written to the file, not
		/// the localized values for male and female (which is what is in the gender combo box).
		/// </summary>
		/// ------------------------------------------------------------------------------------
		private bool HandleBinderTranslateBoundValueBeingSaved(BindingHelper helper,
			Control boundControl, out string newValue)
		{
			newValue = null;

			if (boundControl != _gender)
				return false;

			newValue = (_gender.SelectedIndex == 0 ? "Male" : "Female");
			return true;
		}

		#endregion

		#region Painting methods
		/// ------------------------------------------------------------------------------------
		private void HandleTableLayoutPaint(object sender, PaintEventArgs e)
		{
			DrawUnderlineBelowLabel(e.Graphics, _labelPrimaryLanguage);
			DrawUnderlineBelowLabel(e.Graphics, _labelOtherLanguages);
		}

		/// ------------------------------------------------------------------------------------
		private void DrawUnderlineBelowLabel(Graphics g, Control lbl)
		{
			var col = _tableLayout.GetColumn(lbl);
			var span = _tableLayout.GetColumnSpan(lbl);

			var widths = _tableLayout.GetColumnWidths();

			var lineWidth = 0;
			for (int i = col; i < col + span; i++)
				lineWidth += widths[i];

			lineWidth -= lbl.Margin.Right;
			lineWidth -= _pbPrimaryLangMother.Margin.Right;

			using (var pen = new Pen(SystemColors.ControlDark, 1))
			{
				pen.EndCap = System.Drawing.Drawing2D.LineCap.Square;
				var pt1 = new Point(lbl.Left, lbl.Bottom + 1);
				var pt2 = new Point(lbl.Left + lineWidth, lbl.Bottom + 1);
				g.DrawLine(pen, pt1, pt2);
			}
		}

		#endregion
	}
}
