using System;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;
using DV.Logic.Job;

namespace NoHandbrakeCheck;

public static class Main
{
	private static Harmony? harmony;

	// Unity Mod Manage Wiki: https://wiki.nexusmods.com/index.php/Category:Unity_Mod_Manager
	private static bool Load(UnityModManager.ModEntry modEntry)
	{
		try
		{
			harmony = new Harmony(modEntry.Info.Id);
		}
		catch (Exception ex)
		{
			modEntry.Logger.LogException($"Failed to load {modEntry.Info.DisplayName}:", ex);
			return false;
		}

		modEntry.OnToggle = OnToggle;
		return true;
	}

	private static bool Unload(UnityModManager.ModEntry modEntry)
	{
		harmony?.UnpatchAll(modEntry.Info.Id);
		return true;
	}

	static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled)
	{
		if (harmony == null)
			return false;

		if (enabled)
		{
			try
			{
				harmony.PatchAll(Assembly.GetExecutingAssembly());
			} catch (Exception ex)
			{
				modEntry.Logger.LogException($"Failed to apply patches for {modEntry.Info.DisplayName}:", ex);
				harmony?.UnpatchAll(modEntry.Info.Id);
			}
		}
		else
		{
			harmony?.UnpatchAll(modEntry.Info.Id);
		}

		return true;
	}

	[HarmonyPatch(typeof(TransportTask), "UpdateTaskState")]
	internal class Task_UpdateTaskState_Patch
	{
		private static readonly AccessTools.FieldRef<TransportTask, bool> handbrakeFlag = AccessTools.FieldRefAccess<TransportTask, bool>("anyHandbrakeRequiredAndNotDone");
		private static readonly MethodInfo SetStateMethod = AccessTools.Method(typeof(TransportTask), "SetState");

		private static void Postfix(ref TaskState __result, TransportTask __instance)
		{
			if (!__instance.IsLastTask || __instance.state != TaskState.InProgress)
				return;

			if (handbrakeFlag(__instance))
			{
				handbrakeFlag(__instance) = false;

				SetStateMethod?.Invoke(__instance, new object[] { TaskState.Done });
				__result = TaskState.Done;
			}
		}
	}
}
