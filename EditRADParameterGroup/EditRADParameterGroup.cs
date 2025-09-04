using System;
using System.Collections.Generic;
using System.Linq;
using EditRADParameterGroup;
using RadUtils;
using RadWidgets;
using Skyline.DataMiner.Automation;
using Skyline.DataMiner.Utils.InteractiveAutomationScript;
using Skyline.DataMiner.Utils.RadToolkit;

public class Script
{
	private InteractiveController _app;
	private RadHelper _radHelper;

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
			_radHelper = RadWidgets.Utils.GetRadHelper(engine);

			if (!TryGetRadGroupID(out var settings, out var groupID))
				return;

			if (settings.Subgroups?.Count == 1)
			{
				var dialog = new EditParameterGroupDialog(engine, _radHelper, settings, groupID.DataMinerID);
				dialog.Accepted += (sender, args) => Dialog_Accepted(sender as EditParameterGroupDialog, settings);
				dialog.Cancelled += (sender, args) => Dialog_Cancelled();
				_app.ShowDialog(dialog);
			}
			else
			{
				Guid? subgroupGUID = null;
				if (groupID is RadSubgroupID subgroupID)
				{
					if (subgroupID.SubgroupID != null)
					{
						subgroupGUID = subgroupID.SubgroupID;
					}
					else
					{
						var subgroup = settings.Subgroups.FirstOrDefault(s => string.Equals(s.Name, subgroupID.GroupName, StringComparison.OrdinalIgnoreCase));
						subgroupGUID = subgroup?.ID;
					}
				}

				var dialog = new EditSharedModelGroupDialog(engine, _radHelper, settings, subgroupGUID, groupID.DataMinerID);
				dialog.Accepted += (sender, args) => Dialog_Accepted(sender as EditSharedModelGroupDialog, settings);
				dialog.Cancelled += (sender, args) => Dialog_Cancelled();
				_app.ShowDialog(dialog);
			}
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

	private bool TryGetRadGroupID(out RadGroupInfo settings, out IRadGroupID groupID)
	{
		var groupIDs = RadWidgets.Utils.ParseGroupIDParameter(_app);
		if (groupIDs.Count == 0)
		{
			RadWidgets.Utils.ShowMessageDialog(_app, "No relational anomaly group selected", "Please select the relational anomaly group you want to edit first.");
			settings = null;
			groupID = null;
			return false;
		}
		else if (groupIDs.Count > 1)
		{
			RadWidgets.Utils.ShowMessageDialog(_app, "Multiple relational anomaly groups selected", "Please select a single relational anomaly group you want to edit.");
			settings = null;
			groupID = null;
			return false;
		}

		groupID = groupIDs.First();
		try
		{
			settings = _radHelper.FetchParameterGroupInfo(groupID.DataMinerID, groupID.GroupName);
		}
		catch (Exception ex)
		{
			RadWidgets.Utils.ShowExceptionDialog(_app, "Failed to fetch relational anomaly group information", ex);
			settings = null;
			return false;
		}

		if (settings?.Subgroups == null || settings.Subgroups.Count == 0)
		{
			RadWidgets.Utils.ShowMessageDialog(_app, "No subgroups found", "The selected relational anomaly group does not contain any subgroups to edit.");
			settings = null;
			return false;
		}

		return true;
	}

	private void Dialog_Accepted(EditParameterGroupDialog dialog, RadGroupInfo originalSettings)
	{
		if (dialog == null)
			throw new ArgumentException("Invalid sender type");
		var newSettings = dialog.GroupSettings;
		if (newSettings == null)
			throw new ArgumentException($"{nameof(dialog.GroupSettings)} should not return null", nameof(dialog));

		try
		{
			var originalParameters = originalSettings.Subgroups.First().Parameters.Select(p => p.Key).ToHashSet(new ParameterKeyEqualityComparer());
			if (originalParameters.SetEquals(newSettings.Subgroups.First().Parameters.Select(p => p.Key)))
			{
				if (!originalSettings.GroupName.Equals(newSettings.GroupName, StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						_radHelper.RenameParameterGroup(dialog.DataMinerID, originalSettings.GroupName, newSettings.GroupName);
					}
					catch (NotSupportedException)
					{
						// We can't rename, so remove the old group instead
						_radHelper.RemoveParameterGroup(dialog.DataMinerID, originalSettings.GroupName);
					}
				}
			}
			else
			{
				_radHelper.RemoveParameterGroup(dialog.DataMinerID, originalSettings.GroupName);
			}

			_radHelper.AddParameterGroup(newSettings);
		}
		catch (Exception ex)
		{
			RadWidgets.Utils.ShowExceptionDialog(_app, "Failed to edit relational anomaly group", ex, dialog);
			return;
		}

		_app.Engine.ExitSuccess("Successfully edit relational anomaly group");
	}

	private void Dialog_Accepted(EditSharedModelGroupDialog dialog, RadGroupInfo originalSettings)
	{
		if (dialog == null)
			throw new ArgumentNullException(nameof(dialog), "Invalid sender type");
		var newSettings = dialog.GetGroupSettings();
		if (newSettings == null)
			throw new ArgumentException($"{nameof(dialog.GetGroupSettings)} should not return null", nameof(dialog));

		try
		{
			GetAddedAndRemovedSubgroups(newSettings.Subgroups, originalSettings.Subgroups,
				out List<RadSubgroupSettings> addedSubgroups, out List<RadSubgroupSettings> removedSubgroups);
			if (addedSubgroups.Count == newSettings.Subgroups.Count)
			{
				// No subgroup is preserved, so we remove the entire group
				_radHelper.RemoveParameterGroup(dialog.DataMinerID, originalSettings.GroupName);
			}
			else
			{
				// Add least one of the original subgroups is preserved, so do not remove the entire group
				if (!originalSettings.GroupName.Equals(newSettings.GroupName, StringComparison.OrdinalIgnoreCase))
					_radHelper.RenameParameterGroup(dialog.DataMinerID, originalSettings.GroupName, newSettings.GroupName);

				foreach (var removedSubgroup in removedSubgroups)
					_radHelper.RemoveSubgroup(dialog.DataMinerID, newSettings.GroupName, removedSubgroup.ID);
				foreach (var addedSubgroup in addedSubgroups)
					_radHelper.AddSubgroup(dialog.DataMinerID, newSettings.GroupName, addedSubgroup);
			}

			_radHelper.AddParameterGroup(newSettings);
		}
		catch (Exception ex)
		{
			RadWidgets.Utils.ShowExceptionDialog(_app, "Failed to edit relational anomaly group", ex, dialog);
			return;
		}

		_app.Engine.ExitSuccess("Successfully editted relational anomaly group");
	}

	private void GetAddedAndRemovedSubgroups(List<RadSubgroupSettings> newSubgroups, List<RadSubgroupInfo> oldSubgroups,
		out List<RadSubgroupSettings> addedSubgroups, out List<RadSubgroupSettings> removedSubgroups)
	{
		foreach (var subgroup in oldSubgroups)
			subgroup.NormalizeParameters();

		bool[] matchedOldSubgroups = new bool[oldSubgroups.Count];
		addedSubgroups = new List<RadSubgroupSettings>();
		removedSubgroups = new List<RadSubgroupSettings>();
		foreach (var subgroup in newSubgroups)
		{
			bool matched = false;

			subgroup.NormalizeParameters();
			for (int i = 0; i < oldSubgroups.Count; ++i)
			{
				if (matchedOldSubgroups[i])
					continue;
				if (oldSubgroups[i].HasSameOrderedParameters(subgroup))
				{
					matchedOldSubgroups[i] = true;
					matched = true;
					break;
				}
			}

			if (!matched)
				addedSubgroups.Add(subgroup);
		}

		for (int i = 0; i < matchedOldSubgroups.Length; ++i)
		{
			if (!matchedOldSubgroups[i])
				removedSubgroups.Add(oldSubgroups[i]);
		}
	}

	private void Dialog_Cancelled()
	{
		_app.Engine.ExitSuccess("Editing relational anomaly group cancelled");
	}
}