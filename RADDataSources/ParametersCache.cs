namespace RadDataSources
{
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages;

	/// <summary>
	/// Cache for parameters.
	/// </summary>
	public class ParametersCache : RadUtils.ParametersCache
	{
		private readonly IGQILogger _logger = null;
		private readonly GQIDMS _dms;

		public ParametersCache(IGQILogger logger, GQIDMS dms)
		{
			_logger = logger;
			_dms = dms ?? throw new System.ArgumentNullException(nameof(dms));
		}

		protected override void LogError(string message)
		{
			_logger.Error(message);
		}

		protected override DMSMessage SendSingleResponseMessage(DMSMessage request)
		{
			return _dms.SendMessage(request);
		}
	}
}
