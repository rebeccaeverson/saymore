using System;
using System.Drawing;
using System.Windows.Forms;
using Palaso.Reporting;
using SayMore.UI.Utilities;
using SilTools;
using SilTools.Controls;

namespace SayMore.Transcription.UI
{
	public enum OralAnnotationType
	{
		Careful,
		Translation
	}

	public partial class OralAnnotationRecorder : UserControl, IMessageFilter
	{
		private const int WM_KEYDOWN = 0x100;
		private const int WM_KEYUP = 0x101;

		private readonly string _segmentCountFormatString;
		//private readonly string _micLevelFormatString;
		private OralAnnotationRecorderViewModel _viewModel;
		private string _annotationType;
		private Timer _startTimer;

		/// ------------------------------------------------------------------------------------
		public OralAnnotationRecorder()
		{
			InitializeComponent();

			_segmentCountFormatString = _labelSegmentNumber.Text;
			_labelSegmentNumber.Font = SystemFonts.IconTitleFont;
			//_micLevelFormatString = _labelMicLevel.Text;
			//_labelMicLevel.Font = SystemFonts.IconTitleFont;
			_buttonPlayOriginal.Font = SystemFonts.IconTitleFont;
			_buttonRecord.Font = SystemFonts.IconTitleFont;
			_buttonPlayAnnotation.Font = SystemFonts.IconTitleFont;
			_buttonEraseAnnotation.Font = SystemFonts.IconTitleFont;

			_buttonPlayOriginal.Click += HandleButtonClick;
			_buttonPlayAnnotation.Click += HandleButtonClick;
			_buttonRecord.MouseDown += delegate { UpdateDisplay(); };
			_buttonRecord.MouseUp += delegate { UpdateDisplay(); };

			_buttonPlayOriginal.CanInvokeActionDelegate = (() => !_buttonPlayAnnotation.ActionInProgress);
			_buttonPlayAnnotation.CanInvokeActionDelegate = (() => !_buttonPlayOriginal.ActionInProgress);
			_buttonRecord.CanInvokeActionDelegate = (() => !_buttonPlayOriginal.ActionInProgress);

			_trackBarSegment.ValueChanged += HandleSegmentTrackBarValueChanged;
			_trackBarMicLevel.ValueChanged += delegate { UpdateDisplay(); };

			Application.AddMessageFilter(this);
		}

		/// ------------------------------------------------------------------------------------
		public void Initialize(OralAnnotationRecorderViewModel viewModel, string annotationType)
		{
			_annotationType = annotationType;

			_viewModel = viewModel;
			_viewModel.MicLevelChangeControl = _trackBarMicLevel;
			_viewModel.MicLevelDisplayControl = _panelMicorphoneLevel;
			_viewModel.PlaybackEnded += HandlePlaybackEnded;

			_buttonPlayOriginal.Initialize(" Playing...", "", _viewModel.PlayOriginalRecording, _viewModel.Stop );
			_buttonPlayAnnotation.Initialize(" Playing...", "Check Annotation", _viewModel.PlayAnnotation, _viewModel.Stop);
			_buttonRecord.Initialize(" Recording...", "", _viewModel.BeginRecording, HandleRecordingStopped);

			_trackBarSegment.Minimum = 1;
			_trackBarSegment.Maximum = _viewModel.SegmentCount;
			_trackBarSegment.Value = _viewModel.CurrentSegmentNumber + 1;

			_startTimer = new Timer();
			_startTimer.Interval = 2000;
			_startTimer.Tick += HandleStartTimerTick;
		}

		/// ------------------------------------------------------------------------------------
		private void HandleStartTimerTick(object sender, EventArgs e)
		{
			_startTimer.Tick += HandleStartTimerTick;
			_startTimer.Dispose();
			_startTimer = null;

			Activate(_buttonPlayOriginal);
			_buttonPlayOriginal.InvokeStartAction();
			UpdateDisplay();
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnHandleDestroyed(EventArgs e)
		{
			Application.RemoveMessageFilter(this);

			if (_viewModel != null)
			{
				_viewModel.Stop();
				_viewModel.PlaybackEnded -= HandlePlaybackEnded;
				_viewModel.Dispose();
			}

			ReportUsage();
			base.OnHandleDestroyed(e);
		}

		/// ------------------------------------------------------------------------------------
		public void ReportUsage()
		{
			UsageReporter.SendEvent(Name, _annotationType, "Dialog Opened", null, 0);

			UsageReporter.SendNavigationNotice("{0} - {1}: Playback original invoked {2} times.",
				Name, _annotationType, _buttonPlayOriginal.ActionStartedCount);

			UsageReporter.SendNavigationNotice("{0} - {1}: Playback annotation invoked {2} times.",
				Name, _annotationType, _buttonPlayAnnotation.ActionStartedCount);

			UsageReporter.SendNavigationNotice("{0} - {1}: Record annotation invoked {2} times.",
				Name, _annotationType, _buttonRecord.ActionStartedCount);

			UsageReporter.SendNavigationNotice("{0} - {1}: Stop playback original invoked {2} times.",
				Name, _annotationType, _buttonPlayOriginal.ActionStoppedCount);

			UsageReporter.SendNavigationNotice("{0} - {1}: Stop playback annotation invoked {2} times.",
				Name, _annotationType, _buttonPlayAnnotation.ActionStoppedCount);

			UsageReporter.SendNavigationNotice("{0} - {1}: Erase annotation invoked {2} times.",
				Name, _annotationType, _buttonEraseAnnotation.ActionInvokedCount);
		}

		/// ------------------------------------------------------------------------------------
		public bool PreFilterMessage(ref Message m)
		{
			if (m.Msg != WM_KEYDOWN && m.Msg != WM_KEYUP)
				return false;

			if (m.Msg == WM_KEYUP && (Keys)m.WParam != Keys.Space)
				return false;

			switch ((Keys)m.WParam)
			{
				case Keys.Right: MoveToNextSegment(); break;

				case Keys.Left:
					if (_trackBarSegment.Value > _trackBarSegment.Minimum)
						_trackBarSegment.Value--;
					break;

				//case Keys.O:
				//    if (_buttonPlayOriginal.Enabled)
				//        _buttonPlayOriginal.PerformClick();
				//    break;

				//case Keys.A:
				//    if (_buttonPlayAnnotation.Visible)
				//        _buttonPlayAnnotation.PerformClick();
				//    break;

				case Keys.Space:
					if (_buttonRecord.Visible && _buttonRecord.Active)
					{
						if (m.Msg == WM_KEYUP)
							_buttonRecord.InvokeStopAction();
						else if (m.Msg == WM_KEYDOWN && !_buttonRecord.ActionInProgress)
						{
							Activate(_buttonRecord);
							_buttonRecord.InvokeStartAction();
							UpdateDisplay();
						}
					}
					break;

				case Keys.Tab:
				case Keys.Enter:
				case Keys.Up:
				case Keys.Down:
					// Eat these.
					break;

				default:
					return false;
			}

			return true;
		}

		/// ------------------------------------------------------------------------------------
		private void HandleButtonClick(object sender, EventArgs e)
		{
			if (!_buttonPlayOriginal.ActionInProgress && !_buttonRecord.ActionInProgress &&
				!_buttonPlayAnnotation.ActionInProgress)
			{
				var button = sender as StartStopButton;
				Activate(button);
				button.InvokeStartAction();
				UpdateDisplay();
			}
		}

		/// ------------------------------------------------------------------------------------
		private void HandleEraseButtonClick(object sender, EventArgs e)
		{
			_viewModel.EraseAnnotation();
			Activate(_buttonPlayOriginal);
			_buttonPlayOriginal.InvokeStartAction();
			UpdateDisplay();
		}

		/// ------------------------------------------------------------------------------------
		private void HandleSegmentTrackBarValueChanged(object sender, EventArgs e)
		{
			Activate(_buttonPlayOriginal);

			if (_viewModel.SetCurrentSegmentNumber(_trackBarSegment.Value - 1) &&
				!_viewModel.DoesAnnotationExist)
			{
				_buttonPlayOriginal.InvokeStartAction();
			}

			UpdateDisplay();
		}

		/// ------------------------------------------------------------------------------------
		private void HandlePlaybackEnded(object sender, EventArgs e)
		{
			if (IsDisposed)
				return;

			var buttonToActivate = _buttonPlayOriginal;
			var playbackOfOriginalEnded = (bool)sender;

			if (playbackOfOriginalEnded)
				buttonToActivate = (_viewModel.DoesAnnotationExist ? _buttonPlayAnnotation : _buttonRecord);

			Invoke((Action<StartStopButton>)Activate, buttonToActivate);
			Invoke((Action)UpdateDisplay);
		}

		/// ------------------------------------------------------------------------------------
		private void HandleRecordingStopped()
		{
			_viewModel.Stop();
			if (!MoveToNextSegment())
			{
				Activate(_buttonPlayOriginal);
				UpdateDisplay();
			}
		}

		/// ------------------------------------------------------------------------------------
		private bool MoveToNextSegment()
		{
			if (_trackBarSegment.Value < _trackBarSegment.Maximum)
			{
				_trackBarSegment.Value++;
				return true;
			}

			return false;
		}

		/// ------------------------------------------------------------------------------------
		private void Activate(StartStopButton button)
		{
			_buttonPlayOriginal.Active = (button == _buttonPlayOriginal);
			_buttonPlayAnnotation.Active = (button == _buttonPlayAnnotation);
			_buttonRecord.Active = (button == _buttonRecord);
		}

		/// ------------------------------------------------------------------------------------
		private void UpdateDisplay()
		{
			Utils.SetWindowRedraw(this, false);

			_trackBarSegment.Enabled = !_viewModel.IsRecording;

			_labelSegmentNumber.Text = string.Format(_segmentCountFormatString,
				_trackBarSegment.Value, _viewModel.SegmentCount);

			var state = _viewModel.GetState();
			_buttonPlayOriginal.SetStateProperties(state == OralAnnotationRecorderViewModel.State.PlayingOriginal);
			_buttonPlayAnnotation.SetStateProperties(state == OralAnnotationRecorderViewModel.State.PlayingAnnotation);
			_buttonRecord.SetStateProperties(state == OralAnnotationRecorderViewModel.State.Recording);
			_buttonPlayOriginal.Enabled = (state != OralAnnotationRecorderViewModel.State.Recording);

			_buttonPlayAnnotation.Visible = _viewModel.ShouldListenToAnnotationButtonBeVisible;
			_buttonRecord.Visible = _viewModel.ShouldRecordButtonBeVisible;
			_buttonEraseAnnotation.Visible = _viewModel.ShouldEraseAnnotationButtonBeVisible;
			_buttonEraseAnnotation.Enabled = _viewModel.ShouldEraseAnnotationButtonBeEnabled;

			//_labelMicLevel.Text = string.Format(_micLevelFormatString,_trackBarMicLevel.Value);

			Utils.SetWindowRedraw(this, true);
		}
	}

	#region ActionTrackerButton class
	/// ----------------------------------------------------------------------------------------
	public class ActionTrackerButton : NicerButton
	{
		private bool _active;

		public int ActionInvokedCount { get; private set; }

		/// ------------------------------------------------------------------------------------
		public ActionTrackerButton()
		{
			ShowFocusRectangle = false;
			FlatAppearance.MouseDownBackColor = Color.Transparent;
			FlatAppearance.MouseOverBackColor = Color.Transparent;
		}

		/// ------------------------------------------------------------------------------------
		public bool Active
		{
			get { return _active; }
			set
			{
				_active = value;
				BackColor = (_active ? AppColors.BarBegin : Color.Transparent);
				FlatAppearance.MouseOverBackColor = BackColor;
			}
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnClick(EventArgs e)
		{
			base.OnClick(e);
			ActionInvokedCount++;
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			if (!Active)
				return;

			var rc = ClientRectangle;

			rc.Width--;
			rc.Height--;

			using (var pen = new Pen(AppColors.BarBorder))
				e.Graphics.DrawRectangle(pen, rc);
		}
	}

	#endregion

	#region RecordButton class
	/// ----------------------------------------------------------------------------------------
	public class RecordButton : StartStopButton
	{
		/// ------------------------------------------------------------------------------------
		protected override void OnClick(EventArgs e)
		{
			// Eat it.
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
				InvokeStartAction();

			base.OnMouseDown(e);
		}

		/// ------------------------------------------------------------------------------------
		protected override void OnMouseUp(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
				InvokeStopAction();

			base.OnMouseUp(e);
		}
	}

	#endregion

	#region StartStopButton class
	/// ----------------------------------------------------------------------------------------
	public class StartStopButton : ActionTrackerButton
	{
		private Image _startImage;
		private Image _inProgressImage;
		private string _activeText;
		private string _inactiveText;
		private string _inProgressText;
		private Action _startAction;
		private Action _stopAction;

		public Func<bool> CanInvokeActionDelegate;

		public bool ActionInProgress { get; private set; }
		public int ActionStartedCount { get; private set; }
		public int ActionStoppedCount { get; private set; }

		/// ------------------------------------------------------------------------------------
		public void Initialize(string inProgressText, string inactiveText,
			Action startAction, Action stopAction)
		{
			Cursor = Cursors.Hand;
			_startAction = startAction;
			_stopAction = stopAction;
			_inProgressText = inProgressText;
			_inactiveText = inactiveText;
			_activeText = Text;
			_startImage = Image;
			_inProgressImage = PaintingHelper.MakeHotImage(Image);
		}

		/// ------------------------------------------------------------------------------------
		public void SetStateProperties(bool inProgress)
		{
			Text = (inProgress ? _inProgressText : (Active ? _activeText : _inactiveText));
			Image = (inProgress ? _inProgressImage : _startImage);
			ActionInProgress = inProgress;
			Invalidate();
		}

		/// ------------------------------------------------------------------------------------
		public void InvokeStartAction()
		{
			if (_startAction != null && CanInvokeActionDelegate())
			{
				_startAction();
				ActionStartedCount++;
			}
		}

		/// ------------------------------------------------------------------------------------
		public void InvokeStopAction()
		{
			if (ActionInProgress && _stopAction != null)
			{
				_stopAction();
				ActionStoppedCount++;
			}
		}
	}

	#endregion
}