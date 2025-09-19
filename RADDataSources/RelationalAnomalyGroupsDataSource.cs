namespace RadDataSources
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.DataTypes;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Exceptions;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Utils.RadToolkit;

	/// <summary>
	/// We return a table with the group names, their parameters, updateModel value and AnomalyThreshold for all configured groups.
	/// </summary>
	[GQIMetaData(Name = "Get Relational Anomaly Groups")]
	public class RelationalAnomalyGroupsDataSource : IGQIDataSource, IGQIOnInit, IGQIOnPrepareFetch
	{
		private RadHelper _radHelper;
		private ElementNameCache _elementNames;
		private ParametersCache _parameters;
		private GQIDMS _dms;
		private IGQILogger _logger;
		private IEnumerator<int> _dmaIDEnumerator;

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;
			_radHelper = ConnectionHelper.InitializeRadHelper(_dms, _logger);

			return default;
		}

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Name"),
				new GQIIntColumn("DataMiner Id"),
				new GQIStringColumn("Parameters"),
				new GQIBooleanColumn("Update Model"),
				new GQIDoubleColumn("Anomaly Threshold"),
				new GQITimeSpanColumn("Minimum Anomaly Duration"),
				new GQIBooleanColumn("Is Monitored"),
				new GQIStringColumn("Parent Group"),
				new GQIStringColumn("Subgroup ID"),
			};
		}

		public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			var infoMessages = _dms.SendMessages(new GetInfoMessage(InfoType.DataMinerInfo));
			_dmaIDEnumerator = infoMessages
				.OfType<GetDataMinerInfoResponseMessage>()
				.Select(m => m.ID)
				.Distinct()
				.GetEnumerator();

			_elementNames = new ElementNameCache(_logger, _dms);
			_parameters = new ParametersCache(_logger, _dms);

			return default;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			if (!_dmaIDEnumerator.MoveNext())
				return new GQIPage(Array.Empty<GQIRow>());

			int dataMinerID = _dmaIDEnumerator.Current;
			var groupNames = _radHelper.FetchParameterGroups(dataMinerID);
			if (groupNames == null)
			{
				_logger.Error($"Could not fetch RAD group names from agent {dataMinerID}: no response or response of the wrong type received");
				return new GQIPage(Array.Empty<GQIRow>()) { HasNextPage = true };
			}

			return new GQIPage(groupNames.SelectMany(g => GetRowsForGroup(dataMinerID, g)).ToArray())
			{
				HasNextPage = true,
			};
		}

		private IEnumerable<GQIRow> GetRowsForGroup(int dataMinerID, string groupName)
		{
			RadGroupInfo groupInfo;
			try
			{
				groupInfo = _radHelper.FetchParameterGroupInfo(dataMinerID, groupName);
			}
			catch (DataMinerSecurityException)
			{
				yield break;
			}
			catch (Exception ex)
			{
				_logger.Error($"Failed to fetch RAD group info for group '{groupName}' from agent {dataMinerID}: {ex.Message}");
				throw;
			}

			if (groupInfo == null)
			{
				_logger.Error($"Could not fetch RAD group info for group '{groupName}' from agent {dataMinerID}: no response or response of the wrong type received");
				yield break;
			}

			if (groupInfo.Subgroups == null || groupInfo.Subgroups.Count == 0)
			{
				_logger.Error($"Group '{groupName}' from agent {dataMinerID} has no subgroups defined.");
				yield break;
			}

			bool sharedModelGroup = groupInfo.Subgroups.Count > 1;
			foreach (var subgroupInfo in groupInfo.Subgroups)
			{
				if (subgroupInfo == null)
				{
					_logger.Error($"Subgroup info for group '{groupName}' is null in agent {dataMinerID}.");
					continue;
				}

				double anomalyThreshold;
				int minumumAnomalyDuration;
				if (subgroupInfo.Options != null)
				{
					anomalyThreshold = subgroupInfo.Options.GetAnomalyThresholdOrDefault(_radHelper,
						groupInfo.Options?.AnomalyThreshold);
					minumumAnomalyDuration = subgroupInfo.Options.GetMinimalDurationOrDefault(_radHelper,
						groupInfo.Options?.MinimalDuration);
				}
				else
				{
					anomalyThreshold = _radHelper.DefaultAnomalyThreshold;
					minumumAnomalyDuration = _radHelper.DefaultMinimumAnomalyDuration;
				}

				yield return new GQIRow(
					new GQICell[]
					{
						new GQICell() { Value = subgroupInfo.GetName(groupName) },
						new GQICell() { Value = dataMinerID },
						new GQICell() { Value = ParameterKeysToString(subgroupInfo.Parameters?.Select(p => p?.Key)) },
						new GQICell() { Value = groupInfo.Options?.UpdateModel ?? false },
						new GQICell() { Value = anomalyThreshold },
						new GQICell() { Value = TimeSpan.FromMinutes(minumumAnomalyDuration) },
						new GQICell() { Value = subgroupInfo.IsMonitored },
						new GQICell() { Value = groupName }, // Parent group
						new GQICell() { Value = sharedModelGroup ? subgroupInfo.ID.ToString() : string.Empty }, // Subgroup ID
					});
			}
		}

		private string ParameterKeyToString(ParameterKey pKey)
		{
			if (pKey == null)
				return string.Empty;

			string elementName;
			if (!_elementNames.TryGet(pKey.DataMinerID, pKey.ElementID, out elementName))
				elementName = $"{pKey.DataMinerID}/{pKey.ElementID}";

			_parameters.TryGet(pKey.DataMinerID, pKey.ElementID, out ParameterInfo[] parameters);
			var parameter = parameters?.FirstOrDefault(p => p.ID == pKey.ParameterID);
			var parameterName = parameter?.DisplayName ?? pKey.ParameterID.ToString();

			string instance = string.Empty;
			if (!string.IsNullOrEmpty(pKey.DisplayInstance))
				instance = pKey.DisplayInstance;
			else
				instance = pKey.Instance;

			if (string.IsNullOrEmpty(instance))
				return $"{elementName}/{parameterName}";
			else
				return $"{elementName}/{parameterName}/{instance}";
		}

		private string ParameterKeysToString(IEnumerable<ParameterKey> pKeys)
		{
			if (pKeys == null)
				return string.Empty;

			return $"[{string.Join(", ", pKeys.Select(p => ParameterKeyToString(p)))}]";
		}
	}
}