


using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeFromDuckovCoopMod;




internal class DeferedRunner : MonoBehaviour
{
    static DeferedRunner runner;
    static readonly Queue<Action> tasks = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Debug.Log("==========================================================");
        Debug.Log($"[DeferedRunner] Init() - DLL Version: {BuildInfo.ModVersion}");
        Debug.Log("==========================================================");
        
        if (runner)
        {
            Debug.Log("[DeferedRunner] Already initialized, skipping");
            return;
        }
        var go = new GameObject("[EscapeFromDuckovCoopModDeferedRunner]")
        {
            hideFlags = HideFlags.HideAndDontSave,
        };
        DontDestroyOnLoad(go);
        runner = go.AddComponent<DeferedRunner>();
        runner.StartCoroutine(runner.EofLoop());
        
        Debug.Log("[DeferedRunner] Runner created, calling ModBehaviour initialization");
        
        
        EndOfFrame(() =>
        {
            runner.StartCoroutine(DelayedLog());
        });
        
        
        try
        {
            Debug.Log("[DeferedRunner] Creating ModBehaviour GameObject");
            var modGO = new GameObject("[COOP_ModBehaviour]");
            DontDestroyOnLoad(modGO);
            var modBehaviour = modGO.AddComponent<ModBehaviour>();
            Debug.Log("[DeferedRunner] ModBehaviour component added, OnEnable should be called automatically");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DeferedRunner] Failed to initialize ModBehaviour: {ex.Message}\n{ex.StackTrace}");
        }
    }
    
    static IEnumerator DelayedLog()
    {
        yield return new WaitForSeconds(2f);
        Debug.Log("##########################################################");
        Debug.Log($"[DeferedRunner] DELAYED LOG - Init was called, DLL Version: {BuildInfo.ModVersion}");
        Debug.Log("##########################################################");
    }

    
    
    
    public static void EndOfFrame(Action a)
    {
        tasks.Enqueue(a);
    }

    IEnumerator EofLoop()
    {
        var eof = new WaitForEndOfFrame();
        while (true)
        {
            yield return eof;
            while (tasks.Count > 0)
            {
                SafeInvoke(tasks.Dequeue());
            }
        }
    }

    static void SafeInvoke(Action a)
    {
        try
        {
            a?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DeferedRunner] 延迟任务执行失败: {e}");
        }
    }
}
