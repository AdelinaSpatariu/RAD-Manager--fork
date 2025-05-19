﻿namespace RadWidgets
{
	using System;
	using RadUtils;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	/// <summary>
	/// Editor for RAD group options.
	/// </summary>
	public class RadGroupOptionsEditor : Section
	{
		private readonly CheckBox _updateModelCheckBox;
		private readonly CheckBox _anomalyThresholdOverrideCheckBox;
		private readonly Numeric _anomalyThresholdNumeric;
		private readonly CheckBox _minimalDurationOverrideCheckBox;
		private readonly Time _minimalDurationTime;

		/// <summary>
		/// Initializes a new instance of the <see cref="RadGroupOptionsEditor"/> class.
		/// </summary>
		/// <param name="columnCount">The number of columns the section should take (should be 2 or greater).</param>
		/// <param name="options">The initial settings to display (if any).</param>
		public RadGroupOptionsEditor(int columnCount, RadGroupOptions options = null)
		{
			_updateModelCheckBox = new CheckBox("Update model on new data?")
			{
				IsChecked = options?.UpdateModel ?? false,
				Tooltip = "Whether to continuously update the RAD model when new trend data is available. If not selected, the model will only be trained after " +
				"creation and when you manually specify a training range.",
			};

			_anomalyThresholdOverrideCheckBox = new CheckBox("Override default anomaly threshold?")
			{
				IsChecked = options?.AnomalyThreshold != null,
				Tooltip = "Whether to override the default threshold for detecting anomalies. Anomalies are detected when the anomaly score exceeds this threshold. " +
				"With a high threshold less anomalies will be detected, with a low threshold more anomalies will be detected. If checked, the threshold can be set below.",
			};
			_anomalyThresholdOverrideCheckBox.Changed += (sender, args) => OnAnomalyThresholdOverrideCheckBoxChanged();

			string anomalyThresholdTooltip = "The threshold for detecting anomalies.";
			var anomalyThresholdLabel = new Label("Anomaly threshold")
			{
				Tooltip = anomalyThresholdTooltip,
			};
			_anomalyThresholdNumeric = new Numeric()
			{
				Minimum = 0,
				Value = options?.AnomalyThreshold ?? RadGroupOptions.DefaultAnomalyThreshold,
				StepSize = 0.01,
				IsEnabled = options?.AnomalyThreshold != null,
				Tooltip = anomalyThresholdTooltip,
			};

			_minimalDurationOverrideCheckBox = new CheckBox("Override default minimum anomaly duration?")
			{
				IsChecked = options?.MinimalDuration != null,
				Tooltip = "Whether to override the default duration an anomaly must last before a suggestion event is generated. Note that changing this duration will also have an " +
				"effect on the anomaly score. If checked, the duration can be set below.",
			};
			_minimalDurationOverrideCheckBox.Changed += (sender, args) => OnMinimalDurationOverrideCheckBoxChanged();

			string minimalDurationTooltip = "The minimum duration in minutes an anomaly must last before a suggestion event is generated.";
			var minimalDurationLabel = new Label("Minimum anomaly duration (in minutes)")
			{
				Tooltip = minimalDurationTooltip,
			};
			_minimalDurationTime = new Time()
			{
				HasSeconds = false,
				Minimum = TimeSpan.FromMinutes(5),
				TimeSpan = options?.MinimalDuration != null ? TimeSpan.FromMinutes(options.MinimalDuration.Value) : TimeSpan.FromMinutes(RadGroupOptions.DefaultMinimalDuration),
				ClipValueToRange = true,
				IsEnabled = options?.MinimalDuration != null,
			};

			int row = 0;
			AddWidget(_updateModelCheckBox, row, 0, 1, columnCount);
			++row;

			AddWidget(_anomalyThresholdOverrideCheckBox, row, 0, 1, columnCount);
			++row;

			AddWidget(anomalyThresholdLabel, row, 0);
			AddWidget(_anomalyThresholdNumeric, row, 1, 1, columnCount - 1);
			++row;

			AddWidget(_minimalDurationOverrideCheckBox, row, 0, 1, columnCount);
			++row;

			AddWidget(minimalDurationLabel, row, 0);
			AddWidget(_minimalDurationTime, row, 1, 1, columnCount - 1);
		}

		public RadGroupOptions Options
		{
			get
			{
				return new RadGroupOptions
				{
					UpdateModel = UpdateModel,
					AnomalyThreshold = AnomalyThreshold,
					MinimalDuration = MinimalDuration,
				};
			}
		}

		private bool UpdateModel => _updateModelCheckBox.IsChecked;

		private double? AnomalyThreshold
		{
			get
			{
				if (_anomalyThresholdOverrideCheckBox.IsChecked)
					return _anomalyThresholdNumeric.Value;
				else
					return null;
			}
		}

		private int? MinimalDuration
		{
			get
			{
				if (_minimalDurationOverrideCheckBox.IsChecked)
					return (int)_minimalDurationTime.TimeSpan.TotalMinutes;
				else
					return null;
			}
		}

		private void OnAnomalyThresholdOverrideCheckBoxChanged()
		{
			_anomalyThresholdNumeric.IsEnabled = _anomalyThresholdOverrideCheckBox.IsChecked;
			if (!_anomalyThresholdOverrideCheckBox.IsChecked)
				_anomalyThresholdNumeric.Value = RadGroupOptions.DefaultAnomalyThreshold;
		}

		private void OnMinimalDurationOverrideCheckBoxChanged()
		{
			_minimalDurationTime.IsEnabled = _minimalDurationOverrideCheckBox.IsChecked;
			if (!_minimalDurationOverrideCheckBox.IsChecked)
				_minimalDurationTime.TimeSpan = TimeSpan.FromMinutes(RadGroupOptions.DefaultMinimalDuration);
		}
	}
}
