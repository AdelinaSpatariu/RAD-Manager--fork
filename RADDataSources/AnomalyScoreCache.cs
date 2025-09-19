namespace RadDataSources
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using RadUtils;
	using Skyline.DataMiner.Net.Exceptions;
	using Skyline.DataMiner.Utils.RadToolkit;

	public class AnomalyScoreData
	{
		public AnomalyScoreData(string userDomainName, IRadGroupID groupID,
			DateTime cacheTime, DateTime requestStartTime, DateTime requestEndTime, List<KeyValuePair<DateTime, double>> anomalyScores)
		{
			UserDomainName = userDomainName;
			GroupID = groupID;
			CacheTime = cacheTime;
			RequestStartTime = requestStartTime;
			RequestEndTime = requestEndTime;
			AnomalyScores = anomalyScores ?? new List<KeyValuePair<DateTime, double>>();
		}

		public string UserDomainName { get; set; }

		public IRadGroupID GroupID { get; set; }

		public List<KeyValuePair<DateTime, double>> AnomalyScores { get; set; }

		public DateTime CacheTime { get; set; }

		public DateTime RequestStartTime { get; set; }

		public DateTime RequestEndTime { get; set; }

		public bool IsSameUserAndGroup(string userDomainName, IRadGroupID groupID)
		{
			return UserDomainName == userDomainName && GroupID.Equals(groupID);
		}

		public bool IsValidEntry(string userDomainName, IRadGroupID groupID,
			DateTime startTime, DateTime endTime)
		{
			return IsSameUserAndGroup(userDomainName, groupID) &&
				DateTime.UtcNow <= CacheTime.AddMinutes(5) &&
				startTime >= RequestStartTime.AddMinutes(-5) &&
				endTime <= RequestEndTime.AddMinutes(5);
		}
	}

	public class AnomalyScoreCache
	{
		private const int MAX_CACHE_SIZE = 5;
		private readonly object _anomalyScoreDataLock = new object();
		private readonly List<AnomalyScoreData> _anomalyScoreData = new List<AnomalyScoreData>();

		public List<KeyValuePair<DateTime, double>> GetAnomalyScores(RadHelper helper, IRadGroupID groupID,
			DateTime startTime, DateTime endTime, bool skipCache)
		{
			lock (_anomalyScoreDataLock)
			{
				AnomalyScoreData scoreData = null;

				if (!skipCache)
				{
					scoreData = _anomalyScoreData
						.FirstOrDefault(p => p.IsValidEntry(helper.Connection.UserDomainName, groupID,
						startTime, endTime));
				}

				if (scoreData == null)
				{
					scoreData = UpdateAnomalyScoreData(helper, groupID, startTime, endTime);
				}

				return scoreData.AnomalyScores
						.Where(p => p.Key >= startTime && p.Key <= endTime)
						.ToList();
			}
		}

		private AnomalyScoreData UpdateAnomalyScoreData(RadHelper helper, IRadGroupID groupID, DateTime startTime,
			DateTime endTime)
		{
			DateTime now = DateTime.UtcNow;

			try
			{
				var requestStartTime = Min(now.AddDays(-7), startTime);
				var requestEndTime = Max(now, endTime);
				List<KeyValuePair<DateTime, double>> anomalyScores = null;

				anomalyScores = FetchAnomalyScore(helper, groupID, requestStartTime, requestEndTime);
				if (anomalyScores == null)
					throw new DataMinerCommunicationException("No response or a response of the wrong type received");

				_anomalyScoreData.RemoveAll(p => p.IsSameUserAndGroup(helper.Connection.UserDomainName, groupID));
				if (_anomalyScoreData.Count >= MAX_CACHE_SIZE)
					_anomalyScoreData.RemoveAt(0); // Remove the oldest entry if cache size exceeds limit

				var newScoreData = new AnomalyScoreData(helper.Connection.UserDomainName, groupID,
					now, requestStartTime, requestEndTime, anomalyScores);
				_anomalyScoreData.Add(newScoreData);
				return newScoreData;
			}
			catch (Exception ex)
			{
				throw new DataMinerCommunicationException("Failed to fetch RAD data", ex);
			}
		}

		private List<KeyValuePair<DateTime, double>> FetchAnomalyScore(RadHelper helper, IRadGroupID groupID, DateTime startTime, DateTime endTime)
		{
			try
			{
				if (groupID is RadSubgroupID subgroupID)
				{
					if (subgroupID.SubgroupID != null)
						return helper.FetchAnomalyScoreData(subgroupID.DataMinerID, subgroupID.GroupName, subgroupID.SubgroupID.Value, startTime, endTime);
					else
						return helper.FetchAnomalyScoreData(subgroupID.DataMinerID, subgroupID.GroupName, subgroupID.SubgroupName, startTime, endTime);
				}
			}
			catch (NotSupportedException)
			{
				// If the method is not supported, we fall back to the method without fetching on subgroup
			}

			return helper.FetchAnomalyScoreData(groupID.DataMinerID, groupID.GroupName, startTime, endTime);
		}

		private DateTime Min(DateTime time1, DateTime time2)
		{
			return time1 < time2 ? time1 : time2;
		}

		private DateTime Max(DateTime time1, DateTime time2)
		{
			return time1 > time2 ? time1 : time2;
		}
	}
}
