using System;
using System.Collections.Generic;
using System.Linq;
using RadWidgets;
using RemoveRADParameterGroup;
using Skyline.DataMiner.Analytics.Mad;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Utils.InteractiveAutomationScript;

public class Script
{
	private InteractiveController _app;

	/// <summary>
	/// The Script entry point.
	/// </summary>
	/// <param name="engine">Link with SLAutomation process.</param>
	public void Run(IEngine engine)
	{
		// DO NOT REMOVE THE COMMENTED OUT CODE BELOW OR THE SCRIPT WONT RUN!
		// Interactive scripts need to be launched differently.
		// This is determined by a simple string search looking for "engine.ShowUI" in the source code.
		// However, due to the NuGet package, this string can no longer be detected.
		// This comment is here as a temporary workaround until it has been fixed.
		//// engine.ShowUI();

		try
		{
			_app = new InteractiveController(engine);

			var groupNamesAndIds = Utils.GetGroupNameAndDataMinerID(_app);
			if (groupNamesAndIds.Count == 0)
			{
				Utils.ShowMessageDialog(_app, "No parameter group selected", "Please select the parameter group you want to remove first");
				return;
			}

			var dialog = new RemoveParameterGroupDialog(engine, groupNamesAndIds);
			dialog.Accepted += Dialog_Accepted;
			dialog.Cancelled += Dialog_Cancelled;

			_app.ShowDialog(dialog);
		}
		catch (ScriptAbortException)
		{
			throw;
		}
		catch (ScriptForceAbortException)
		{
			throw;
		}
		catch (ScriptTimeoutException)
		{
			throw;
		}
		catch (InteractiveUserDetachedException)
		{
			throw;
		}
		catch (Exception e)
		{
			engine.ExitFail(e.ToString());
		}
	}

	private void Dialog_Cancelled(object sender, EventArgs e)
	{
		_app.Engine.ExitSuccess("Removing parameter group cancelled");
	}

	private void Dialog_Accepted(object sender, EventArgs e)
	{
		var dialog = sender as RemoveParameterGroupDialog;
		if (dialog == null)
			throw new ArgumentException("Invalid sender type");

		var failedGroups = new List<Tuple<string, Exception>>();
		foreach (var group in dialog.GroupNamesAndIDs)
		{
			try
			{
				var message = new RemoveMADParameterGroupMessage(group.Item2)
				{
					DataMinerID = group.Item1,
				};
				_app.Engine.SendSLNetSingleResponseMessage(message);
			}
			catch (Exception ex)
			{
				_app.Engine.GenerateInformation($"Failed to remove parameter group '{group.Item2}': {ex}");
				failedGroups.Add(Tuple.Create(group.Item2, ex));
			}
		}

		if (failedGroups.Count > 0)
		{
			var ex = new AggregateException("Failed to remove parameter group(s) from RAD configuration", failedGroups.Select(p => p.Item2));
			Utils.ShowExceptionDialog(_app, $"Failed to remove {failedGroups.Select(p => p.Item1).HumanReadableJoin()}", ex);
			return;
		}

		_app.Engine.ExitSuccess("Successfully removed parameter group from RAD configuration");
	}
}
