﻿namespace RadWidgets
{
	using RadUtils;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

	public class RadSubgroupOptionsEditor : Section
    {
        private readonly RadGroupBaseOptionsEditor _baseOptionsEditor;

        public RadSubgroupOptionsEditor(int columnCount, double? parentAnomalyThreshold, int? parentMinimalDuration, RadSubgroupOptions options = null)
        {
            _baseOptionsEditor = new RadGroupBaseOptionsEditor(columnCount, options,
				parentAnomalyThreshold.GetValueOrDefault(RadGroupOptions.DefaultAnomalyThreshold),
				parentMinimalDuration.GetValueOrDefault(RadGroupOptions.DefaultMinimalDuration));

            AddSection(_baseOptionsEditor, 0, 0);
        }

        public RadSubgroupOptions Options
        {
            get
            {
                return new RadSubgroupOptions
                {
                    AnomalyThreshold = _baseOptionsEditor.AnomalyThreshold,
                    MinimalDuration = _baseOptionsEditor.MinimalDuration,
                };
            }
        }
    }
}
