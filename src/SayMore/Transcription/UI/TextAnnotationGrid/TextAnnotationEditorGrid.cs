using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Localization;
using SayMore.Model.Files;
using SayMore.Properties;
using SayMore.Transcription.Model;
using SayMore.UI.MediaPlayer;
using SilTools;

namespace SayMore.Transcription.UI
{
	public class TextAnnotationEditorGrid : SilGrid
	{
		public Func<Segment> SegmentProvider;
		public Func<OralAnnotationType, IEnumerable<AnnotationPlaybackInfo>> AnnotationPlaybackInfoProvider;
		public MediaPlayerViewModel PlayerViewModel { get; private set; }
		public bool PlaybackInProgress { get; private set; }

		private AnnotationComponentFile _annotationFile;
		private List<AnnotationPlaybackInfo> _mediaFileQueue = new List<AnnotationPlaybackInfo>();
		private bool _autoResizingRowInProgress;
		private int _annotationPlaybackLoopCount;
		private Action _playbackProgressReportingAction;

		private System.Threading.Timer _delayBeginRowPlayingTimer;

		/// ------------------------------------------------------------------------------------
		public TextAnnotationEditorGrid()
		{
			Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
			Margin = new Padding(0);
			VirtualMode = true;
			ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
			AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None; // AllCellsExceptHeaders;
			AllowUserToResizeRows = false;
			EditMode = DataGridViewEditMode.EditOnEnter;
			ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable;
			FullRowFocusRectangleColor = DefaultCellStyle.SelectionBackColor;
			DefaultCellStyle.SelectionForeColor = DefaultCellStyle.ForeColor;
			DefaultCellStyle.SelectionBackColor =
				ColorHelper.CalculateColor(Color.White, DefaultCellStyle.SelectionBackColor, 140);

			PlayerViewModel = new MediaPlayerViewModel();
			PlayerViewModel.SetVolume(100);
			PlayerViewModel.SetSpeed(Settings.Default.AnnotationEditorPlaybackSpeedIndex);

			SetPlaybackProgressReportAction(null);
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnRowHeightInfoNeeded(DataGridViewRowHeightInfoNeededEventArgs e)
		{
			base.OnRowHeightInfoNeeded(e);

			if (_autoResizingRowInProgress)
				return;

			_autoResizingRowInProgress = true;
			AutoResizeRow(e.RowIndex);
			e.Height = Math.Max(Rows[e.RowIndex].Height, 25);
			_autoResizingRowInProgress = false;
		}

		/// ------------------------------------------------------------------------------------
		public void Load(AnnotationComponentFile file)
		{
			_annotationFile = file;

			Utils.SetWindowRedraw(this, false);
			RowCount = 0;
			Columns.Clear();

			if (_annotationFile == null)
				return;

			int rowCount = 0;

			foreach (var tier in _annotationFile.Tiers)
				rowCount = Math.Max(rowCount, AddColumnForTier(tier));

			RowCount = rowCount;
			Utils.SetWindowRedraw(this, true);
			Invalidate();

			if (Settings.Default.SegmentGrid != null)
				Settings.Default.SegmentGrid.InitializeGrid(this);

			AutoResizeColumnHeadersHeight();
			ColumnHeadersHeight += 8;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Set the initial width of text annotation columns so they fill the available,
		/// visible, grid space.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		public void FirstTimeColumnInitialization()
		{
			// If the grid already has settings saved, then don't
			// adjust the column widths to fit the available width.
			if (Settings.Default.SegmentGrid != null)
				return;

			var annotationCols = Columns.OfType<TextAnnotationColumn>().ToArray();

			var widthOfOtherCols = Columns.Cast<DataGridViewColumn>()
				.Where(col => !(col is TextAnnotationColumn)).Sum(col => col.Width);

			var availableWidthForAnnotationCols = ClientSize.Width - widthOfOtherCols -
				RowHeadersWidth - SystemInformation.VerticalScrollBarWidth;

			// Distribute the annotation columns evenly within the available space.
			foreach (var col in annotationCols)
				col.Width = availableWidthForAnnotationCols / annotationCols.Length - 1;
		}

		/// ------------------------------------------------------------------------------------
		private int AddColumnForTier(TierBase tier)
		{
			Columns.Add(tier.GridColumn);

			var col = tier.GridColumn as TextAnnotationColumn;
			if (col != null)
				col.SegmentChangedAction = _annotationFile.Save;

			return tier.Segments.Count();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// When the user is in a transcription cell, this will intercept the tab and shift+tab
		/// keys so they move to the next transcription cell or previous transcription cell
		/// respectively.
		/// </summary>
		/// ------------------------------------------------------------------------------------
		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (IsCurrentCellInEditMode && msg.WParam.ToInt32() == (int)Keys.Tab)
			{
				int newRowIndex = CurrentCellAddress.Y + (ModifierKeys == Keys.Shift ? -1 : 1);

				if (newRowIndex >= 0 && newRowIndex < RowCount)
				{
					EndEdit();
					CurrentCell = this[CurrentCell.ColumnIndex, newRowIndex];
				}

				return true;
			}

			return base.ProcessCmdKey(ref msg, keyData);
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnColumnWidthChanged(DataGridViewColumnEventArgs e)
		{
			base.OnColumnWidthChanged(e);
			AutoResizeColumnHeadersHeight();
			ColumnHeadersHeight += 8;
			Settings.Default.SegmentGrid = GridSettings.Create(this);
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			if (ColumnCount > 0)
				return;

			var hint = LocalizationManager.GetString("EventsView.Transcription.TextAnnotationEditor.NoTranscriptionAnnotationsFoundMsg",
				"There are no transcription annotations found in\n'{0}'", "Parameter is file name.");

			DrawMessageInCenterOfGrid(e.Graphics, string.Format(hint,
				Path.GetFileName(_annotationFile.PathToAnnotatedFile)), 0);
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnCellMouseClick(DataGridViewCellMouseEventArgs e)
		{
			base.OnCellMouseClick(e);

			if (e.ColumnIndex < 0 || e.RowIndex < 0 || e.Button != MouseButtons.Right)
				return;

			var col = Columns[e.ColumnIndex] as TierColumnBase;

			if (col == null)
				return;

			var menuItems = col.GetContextMenuCommands().ToArray();
			if (menuItems.Length == 0)
				return;

			var menu = new ContextMenuStrip();
			menu.Items.AddRange(menuItems.ToArray());
			menu.Show(MousePosition);
		}

		#region Playback methods
		/// ------------------------------------------------------------------------------------
		public void SetPlaybackSpeed(int playbackSpeed)
		{
			if (PlayerViewModel.Speed != playbackSpeed)
				PlayerViewModel.SetSpeed(playbackSpeed);
		}

		/// ------------------------------------------------------------------------------------
		public void SetPlaybackProgressReportAction(Action action)
		{
			_playbackProgressReportingAction = (action ?? (() => { }));
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnCellEnter(DataGridViewCellEventArgs e)
		{
			Stop();

			base.OnCellEnter(e);

			if (CurrentCellAddress.Y < 0 || (!Focused && (EditingControl == null || !EditingControl.Focused)))
				return;

			// Now that we're on a new row, wait a 1/4 of a second before beginning to
			// play this row's media segment. Do this just in case the user is moving
			// from row to row rapidly. Before the 1/4 sec. delay, the program's
			// responsiveness to moving from row to row rapidly was very sluggish. This
			// forces the user to settle on a row, at least briefly, before we attempt
			// to begin playback.
			_delayBeginRowPlayingTimer = new System.Threading.Timer(
				a => Play(), null, 250, System.Threading.Timeout.Infinite);
		}

		/// ------------------------------------------------------------------------------------
		private void DisableTimer()
		{
			if (_delayBeginRowPlayingTimer != null)
			{
				_delayBeginRowPlayingTimer.Dispose();
				_delayBeginRowPlayingTimer = null;
			}
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnEnter(EventArgs e)
		{
			base.OnEnter(e);

			if (!PlayerViewModel.HasPlaybackStarted)
				Play();
		}

		/// ------------------------------------------------------------------------------------
		public void Play()
		{
			Play(true);
		}

		/// ------------------------------------------------------------------------------------
		public void Play(bool resetLoopCounter)
		{
			if (RowCount == 0)
				return;

			DisableTimer();

			if (PlayerViewModel.HasPlaybackStarted)
				Stop();

			if (resetLoopCounter)
				_annotationPlaybackLoopCount = 0;

			Debug.Assert(AnnotationPlaybackInfoProvider != null);

			if (AnnotationPlaybackInfoProvider == null)
				return;

			var currCol = Columns[CurrentCellAddress.X] as TextAnnotationColumnWithMenu;
			var playbackType = (currCol != null ? currCol.PlaybackType : OralAnnotationType.Original);
			_mediaFileQueue = AnnotationPlaybackInfoProvider(playbackType).ToList();
			InternalPlay();
			PlaybackInProgress = true;
		}

		/// ------------------------------------------------------------------------------------
		private void InternalPlay()
		{
			if (_mediaFileQueue.Count == 0)
				return;

			PlayerViewModel.PlaybackStarted -= HandleMediaPlayStarted;
			PlayerViewModel.PlaybackEnded -= HandleMediaPlaybackEnded;

			if (_mediaFileQueue[0].Length > 0f)
				PlayerViewModel.LoadFile(_mediaFileQueue[0].MediaFile, _mediaFileQueue[0].Start, _mediaFileQueue[0].Length);
			else
			{
				PlayerViewModel.LoadFile(_mediaFileQueue[0].MediaFile);
				_mediaFileQueue[0].End = PlayerViewModel.GetTotalMediaDuration();
				_mediaFileQueue[0].Length = _mediaFileQueue[0].End;
			}

			PlayerViewModel.PlaybackStarted += HandleMediaPlayStarted;
			PlayerViewModel.PlaybackEnded += HandleMediaPlaybackEnded;
			PlayerViewModel.PlaybackPositionChanged = (pos => Invoke(_playbackProgressReportingAction));
			PlayerViewModel.Play();
		}

		/// ------------------------------------------------------------------------------------
		public void Stop()
		{
			PlaybackInProgress = false;
			_annotationPlaybackLoopCount = 0;

			DisableTimer();
			PlayerViewModel.PlaybackStarted -= HandleMediaPlayStarted;
			PlayerViewModel.PlaybackEnded -= HandleMediaPlaybackEnded;
			PlayerViewModel.PlaybackPositionChanged = null;
			PlayerViewModel.Stop();
			_mediaFileQueue.Clear();
		}

		/// ------------------------------------------------------------------------------------
		private void HandleMediaPlaybackEnded(object sender, bool EndedBecauseEOF)
		{
			if (InvokeRequired)
				Invoke(_playbackProgressReportingAction);
			else
				_playbackProgressReportingAction();

			if (!EndedBecauseEOF)
				return;

			_mediaFileQueue.RemoveAt(0);

			if (_mediaFileQueue.Count > 0)
				InternalPlay();
			else if (_annotationPlaybackLoopCount++ < 4)
				Play(false);
			else
			{
				PlaybackInProgress = false;
				if (InvokeRequired)
					Invoke(_playbackProgressReportingAction);
				else
					_playbackProgressReportingAction();
			}
		}

		/// ------------------------------------------------------------------------------------
		private void HandleMediaPlayStarted(object sender, EventArgs e)
		{
			if (InvokeRequired)
				Invoke(_playbackProgressReportingAction);
			else
				_playbackProgressReportingAction();
		}

		/// ------------------------------------------------------------------------------------
		public void DrawPlaybackProgressBar(Graphics g, Rectangle rc, Color baseBackColor)
		{
			if (_mediaFileQueue.Count == 0 || !PlaybackInProgress)
				return;

			var playbackPosition = PlayerViewModel.CurrentPosition;
			if (playbackPosition.Equals(0f))
				return;

			var start = _mediaFileQueue[0].Start;
			var end = _mediaFileQueue[0].End;
			var length = Math.Round(end - start, 1, MidpointRounding.AwayFromZero);
			var pixelsPerSec = rc.Width / length;
			rc.Width = (int)Math.Ceiling(pixelsPerSec * (playbackPosition - start));

			if (rc.Width <= 0)
				return;

			rc.Height -= 6;
			rc.Y += 3;
			using (var br = new SolidBrush(ColorHelper.CalculateColor(Color.White, baseBackColor, 110)))
				g.FillRectangle(br, rc);
		}

		#endregion
	}
}