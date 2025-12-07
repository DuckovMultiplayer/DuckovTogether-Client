















using System.Text;
using Duckov.Scenes;
using Duckov.UI;
using Duckov.Utilities;
using ItemStatsSystem;
using ItemStatsSystem.Items;
using Object = UnityEngine.Object;

namespace EscapeFromDuckovCoopMod;

public class LootNet
{
    public readonly Dictionary<uint, Item> _cliPendingPut = new();

    private readonly Dictionary<uint, Item> _cliPendingSlotPlug = new();

    public readonly Dictionary<Item, (Item newItem,
            Inventory destInv, int destPos,
            Slot destSlot)>
        _cliSwapByVictim = new();

    
    public bool _applyingLootState; 

    
    public uint _nextLootToken = 1;
    public bool _serverApplyingLoot; 
    private NetService Service => NetService.Instance;

    private bool IsServer => Service != null && Service.IsServer;
    private NetManager netManager => Service?.netManager;
    private NetDataWriter writer => Service?.writer;
    private NetPeer connectedPeer => Service?.connectedPeer;
    private PlayerStatus localPlayerStatus => Service?.localPlayerStatus;

    private bool networkStarted => Service != null && Service.networkStarted;

    
    public bool ApplyingLootState => _applyingLootState;

    private uint _cliLocalToken
    {
        get => _nextLootToken;
        set => _nextLootToken = value;
    }

    public void Client_RequestLootState(Inventory lootInv)
    {
        if (!networkStarted || IsServer || connectedPeer == null || lootInv == null) return;

        if (LootboxDetectUtil.IsPrivateInventory(lootInv)) return;

        Vector3 pos;
        if (!LootManager.Instance.TryGetLootboxWorldPos(lootInv, out pos)) pos = Vector3.zero;

        var msg = new Net.HybridNet.LootRequestOpenMessage
        {
            LootUid = LootManager.Instance.GetLootUid(lootInv),
            Scene = LootManager.Instance.GetLootScene(lootInv),
            PositionHint = pos
        };
        Net.HybridNet.HybridNetCore.Send(msg, connectedPeer);
    }


    
    public void Server_SendLootboxState(NetPeer toPeer, Inventory inv)
    {
        
        if (toPeer == null && LootManager.Instance.Server_IsLootMuted(inv)) return;

        if (!IsServer || inv == null) return;
        if (!LootboxDetectUtil.IsLootboxInventory(inv) || LootboxDetectUtil.IsPrivateInventory(inv))
            return;

        var msg = new Net.HybridNet.LootStateMessage
        {
            LootUid = LootManager.Instance.GetLootUid(inv),
            ContainerSnapshot = LootManager.Instance.SerializeLootState(inv)
        };
        if (toPeer != null)
            Net.HybridNet.HybridNetCore.Send(msg, toPeer);
        else
            Net.HybridNet.HybridNetCore.Send(msg);
    }


    public void Client_ApplyLootboxState(NetDataReader r)
    {
        var scene = r.GetInt();
        var posKey = r.GetInt();
        var iid = r.GetInt();
        var lootUid = r.GetInt();

        var capacity = r.GetInt();
        var count = r.GetInt();

        Inventory inv = null;

        
        if (lootUid >= 0 && LootManager.Instance._cliLootByUid.TryGetValue(lootUid, out var byUid) && byUid) inv = byUid;

        
        if (inv == null && (!LootManager.Instance.TryResolveLootById(scene, posKey, iid, out inv) || inv == null))
        {
            if (LootboxDetectUtil.IsPrivateInventory(inv)) return;
            
            var list = new List<(int pos, ItemSnapshot snap)>(count);
            for (var k = 0; k < count; ++k)
            {
                var p = r.GetInt();
                var snap = ItemTool.ReadItemSnapshot(r);
                list.Add((p, snap));
            }

            if (lootUid >= 0) LootManager.Instance._pendingLootStatesByUid[lootUid] = (capacity, list);

            
            return;
        }

        if (LootboxDetectUtil.IsPrivateInventory(inv)) return;

        
        capacity = Mathf.Clamp(capacity, 1, 128);

        _applyingLootState = true;
        try
        {
            inv.SetCapacity(capacity);
            inv.Loading = false;

            for (var i = inv.Content.Count - 1; i >= 0; --i)
            {
                Item removed;
                inv.RemoveAt(i, out removed);
                if (removed) Object.Destroy(removed.gameObject);
            }

            for (var k = 0; k < count; ++k)
            {
                var pos = r.GetInt();
                var snap = ItemTool.ReadItemSnapshot(r);
                var item = ItemTool.BuildItemFromSnapshot(snap);
                if (item == null) continue;
                inv.AddAt(item, pos);
            }
        }
        finally
        {
            _applyingLootState = false;
        }


        try
        {
            var lv = LootView.Instance;
            if (lv && lv.open && ReferenceEquals(lv.TargetInventory, inv))
            {
                
                AccessTools.Method(typeof(LootView), "RefreshDetails")?.Invoke(lv, null);
                AccessTools.Method(typeof(LootView), "RefreshPickAllButton")?.Invoke(lv, null);
                AccessTools.Method(typeof(LootView), "RefreshCapacityText")?.Invoke(lv, null);
            }
        }
        catch
        {
        }
    }


    
    public void Client_SendLootPutRequest(Inventory lootInv, Item item, int preferPos)
    {
        if (!networkStarted || IsServer || connectedPeer == null || lootInv == null || item == null) return;

        if (LootboxDetectUtil.IsPrivateInventory(lootInv)) return;


        
        foreach (var kv in _cliPendingPut)
        {
            var pending = kv.Value;
            if (pending && ReferenceEquals(pending, item))
            {
                
                Debug.Log($"[LOOT] Duplicate PUT suppressed for item: {item.DisplayName}");
                return;
            }
        }

        var token = _nextLootToken++;
        _cliPendingPut[token] = item;

        var msg = new Net.HybridNet.LootRequestPutMessage
        {
            LootUid = LootManager.Instance.GetLootUid(lootInv),
            PreferPos = preferPos,
            Token = (int)token,
            ItemSnapshot = ""
        };
        Net.HybridNet.HybridNetCore.Send(msg, connectedPeer);
    }


    
    
    public void Client_SendLootTakeRequest(Inventory lootInv, int position)
    {
        Client_SendLootTakeRequest(lootInv, position, null, -1, null);
    }

    
    public uint Client_SendLootTakeRequest(
        Inventory lootInv,
        int position,
        Inventory destInv,
        int destPos,
        Slot destSlot)
    {
        if (!networkStarted || IsServer || connectedPeer == null || lootInv == null) return 0;
        if (LootboxDetectUtil.IsPrivateInventory(lootInv)) return 0;

        
        if (destInv != null && LootboxDetectUtil.IsLootboxInventory(destInv))
            destInv = null;

        var token = _nextLootToken++;

        if (destInv != null || destSlot != null)
            LootManager.Instance._cliPendingTake[token] = new PendingTakeDest
            {
                inv = destInv,
                pos = destPos,
                slot = destSlot,
                
                srcLoot = lootInv,
                srcPos = position
            };

        var msg = new Net.HybridNet.LootRequestTakeMessage
        {
            LootUid = LootManager.Instance.GetLootUid(lootInv),
            Position = position,
            Token = (int)token
        };
        Net.HybridNet.HybridNetCore.Send(msg, connectedPeer);
        return token;
    }


    
    public void Server_HandleLootOpenRequestFromMessage(NetPeer peer, Net.HybridNet.LootRequestOpenMessage msg)
    {
        
    }
    
    public void Server_HandleLootPutRequestFromMessage(NetPeer peer, Net.HybridNet.LootRequestPutMessage msg)
    {
        
    }
    
    public void Server_HandleLootTakeRequestFromMessage(NetPeer peer, Net.HybridNet.LootRequestTakeMessage msg)
    {
        
    }
    
    public void Server_HandleLootSplitRequestFromMessage(NetPeer peer, Net.HybridNet.LootRequestSplitMessage msg)
    {
        
    }
    
    public void Server_HandleLootSlotPlugRequestFromMessage(NetPeer peer, Net.HybridNet.LootSlotPlugMessage msg)
    {
        
    }
    
    public void Server_HandleLootSlotUnplugRequestFromMessage(NetPeer peer, Net.HybridNet.LootSlotUnplugMessage msg)
    {
        
    }

    
    public void Server_HandleLootPutRequest(NetPeer peer, NetDataReader r)
    {
        var scene = r.GetInt();
        var posKey = r.GetInt();
        var iid = r.GetInt();
        var lootUid = r.GetInt(); 
        var prefer = r.GetInt();
        var token = r.GetUInt();

        ItemSnapshot snap;
        try
        {
            snap = ItemTool.ReadItemSnapshot(r);
        }
        catch (DecoderFallbackException ex)
        {
            Debug.LogError($"[LOOT][PUT] snapshot decode failed: {ex.Message}");
            Server_SendLootDeny(peer, "bad_snapshot");
            return;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LOOT][PUT] snapshot parse failed: {ex}");
            Server_SendLootDeny(peer, "bad_snapshot");
            return;
        }

        
        Inventory inv = null;
        if (lootUid >= 0) LootManager.Instance._srvLootByUid.TryGetValue(lootUid, out inv);
        if (inv == null && !LootManager.Instance.TryResolveLootById(scene, posKey, iid, out inv))
        {
            Server_SendLootDeny(peer, "no_inv");
            return;
        }

        if (LootboxDetectUtil.IsPrivateInventory(inv))
        {
            Server_SendLootDeny(peer, "no_inv");
            return;
        }

        var item = ItemTool.BuildItemFromSnapshot(snap);
        if (item == null)
        {
            Server_SendLootDeny(peer, "bad_item");
            return;
        }

        _serverApplyingLoot = true;
        var ok = false;
        try
        {
            ok = inv.AddAndMerge(item, prefer);
            if (!ok) Object.Destroy(item.gameObject);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LOOT][PUT] AddAndMerge exception: {ex}");
            ok = false;
        }
        finally
        {
            _serverApplyingLoot = false;
        }

        if (!ok)
        {
            Server_SendLootDeny(peer, "add_fail");
            return;
        }

        var ack = new Net.HybridNet.LootPutOkMessage
        {
            Token = (int)token
        };
        Net.HybridNet.HybridNetCore.Send(ack, peer);

        Server_SendLootboxState(null, inv);
    }


    public void Server_HandleLootTakeRequest(NetPeer peer, NetDataReader r)
    {
        var scene = r.GetInt();
        var posKey = r.GetInt();
        var iid = r.GetInt();
        var lootUid = r.GetInt(); 
        var position = r.GetInt();
        var token = r.GetUInt(); 

        Inventory inv = null;
        if (lootUid >= 0) LootManager.Instance._srvLootByUid.TryGetValue(lootUid, out inv);
        if (inv == null && !LootManager.Instance.TryResolveLootById(scene, posKey, iid, out inv))
        {
            Server_SendLootDeny(peer, "no_inv");
            return;
        }

        if (LootboxDetectUtil.IsPrivateInventory(inv))
        {
            Server_SendLootDeny(peer, "no_inv");
            return;
        }


        _serverApplyingLoot = true;
        var ok = false;
        Item removed = null;
        try
        {
            if (position >= 0 && position < inv.Capacity)
                try
                {
                    ok = inv.RemoveAt(position, out removed);
                }
                catch (ArgumentOutOfRangeException)
                {
                    ok = false;
                    removed = null;
                }
        }
        finally
        {
            _serverApplyingLoot = false;
        }

        if (!ok || removed == null)
        {
            Server_SendLootDeny(peer, "rm_fail");
            Server_SendLootboxState(peer, inv); 
            return;
        }

        var wCli = new Net.HybridNet.LootTakeOkMessage
        {
            Token = (int)token,
            ItemSnapshot = ""
        };
        Net.HybridNet.HybridNetCore.Send(wCli, peer);

        try
        {
            Object.Destroy(removed.gameObject);
        }
        catch
        {
        }

        Server_SendLootboxState(null, inv);
    }

    public void Server_SendLootDeny(NetPeer peer, string reason)
    {
        var deny = new Net.HybridNet.LootDenyMessage
        {
            Token = 0,
            Reason = reason ?? ""
        };
        Net.HybridNet.HybridNetCore.Send(deny, peer);
    }

    
    public void Client_OnLootPutOk(NetDataReader r)
    {
        var token = r.GetUInt();

        if (_cliPendingSlotPlug.TryGetValue(token, out var victim) && victim)
        {
            try
            {
                var srcInv = victim.InInventory;
                if (srcInv)
                    try
                    {
                        srcInv.RemoveItem(victim);
                    }
                    catch
                    {
                    }

                Object.Destroy(victim.gameObject);
            }
            catch
            {
            }
            finally
            {
                _cliPendingSlotPlug.Remove(token);
            }

            return; 
        }

        if (_cliPendingPut.TryGetValue(token, out var localItem) && localItem)
        {
            _cliPendingPut.Remove(token);

            
            if (_cliSwapByVictim.TryGetValue(localItem, out var ctx))
            {
                _cliSwapByVictim.Remove(localItem);

                
                try
                {
                    localItem.Detach();
                }
                catch
                {
                }

                try
                {
                    Object.Destroy(localItem.gameObject);
                }
                catch
                {
                }

                
                try
                {
                    if (ctx.destSlot != null)
                    {
                        if (ctx.destSlot.CanPlug(ctx.newItem))
                            ctx.destSlot.Plug(ctx.newItem, out _);
                    }
                    else if (ctx.destInv != null && ctx.destPos >= 0)
                    {
                        
                        ctx.destInv.AddAt(ctx.newItem, ctx.destPos);
                    }
                }
                catch
                {
                }

                
                var toRemove = new List<uint>();
                foreach (var kv in _cliPendingPut)
                    if (!kv.Value || ReferenceEquals(kv.Value, localItem))
                        toRemove.Add(kv.Key);
                foreach (var k in toRemove) _cliPendingPut.Remove(k);

                return; 
            }

            
            try
            {
                localItem.Detach();
            }
            catch
            {
            }

            try
            {
                Object.Destroy(localItem.gameObject);
            }
            catch
            {
            }

            var stale = new List<uint>();
            foreach (var kv in _cliPendingPut)
                if (!kv.Value || ReferenceEquals(kv.Value, localItem))
                    stale.Add(kv.Key);
            foreach (var k in stale) _cliPendingPut.Remove(k);
        }
    }


    public void Client_OnLootTakeOk(NetDataReader r)
    {
        var token = r.GetUInt();

        
        var snap = ItemTool.ReadItemSnapshot(r);
        var newItem = ItemTool.BuildItemFromSnapshot(snap);
        if (newItem == null) return;

        
        PendingTakeDest dest;
        if (LootManager.Instance._cliPendingTake.TryGetValue(token, out dest))
            LootManager.Instance._cliPendingTake.Remove(token);
        else
            dest = default;

        
        
        void PutBackToSource_NoTrack(Item item, PendingTakeDest srcInfo)
        {
            var loot = srcInfo.srcLoot != null ? srcInfo.srcLoot
                : LootView.Instance ? LootView.Instance.TargetInventory : null;
            var preferPos = srcInfo.srcPos >= 0 ? srcInfo.srcPos : -1;

            try
            {
                if (networkStarted && !IsServer && connectedPeer != null && loot != null && item != null)
                {
                    var w = writer;
                    if (w == null) return;
                    w.Reset();
                    LootManager.Instance.PutLootId(w, loot);
                    w.Put(preferPos);
                    w.Put((uint)0); 
                    ItemTool.WriteItemSnapshot(w, item);
                    connectedPeer.Send(w, DeliveryMethod.ReliableOrdered);
                }
            }
            catch
            {
            }

            
            try
            {
                item.Detach();
            }
            catch
            {
            }

            try
            {
                Object.Destroy(item.gameObject);
            }
            catch
            {
            }

            
            try
            {
                var lv = LootView.Instance;
                var inv = lv ? lv.TargetInventory : null;
                if (inv) Client_RequestLootState(inv);
            }
            catch
            {
            }
        }

        
        if (LootManager.Instance._cliPendingReorder.TryGetValue(token, out var reo))
        {
            LootManager.Instance._cliPendingReorder.Remove(token);
            Client_SendLootPutRequest(reo.inv, newItem, reo.pos);
            return;
        }

        
        if (dest.slot != null)
        {
            Item victim = null;
            try
            {
                victim = dest.slot.Content;
            }
            catch
            {
            }

            if (victim != null)
            {
                _cliSwapByVictim[victim] = (newItem, null, -1, dest.slot);
                var srcLoot = dest.srcLoot ?? (LootView.Instance ? LootView.Instance.TargetInventory : null);
                Client_SendLootPutRequest(srcLoot, victim, dest.srcPos);
                return;
            }

            try
            {
                if (dest.slot.CanPlug(newItem) && dest.slot.Plug(newItem, out _))
                    return; 
            }
            catch
            {
            }

            
            PutBackToSource_NoTrack(newItem, dest);
            return;
        }

        
        if (dest.inv != null)
        {
            Item victim = null;
            try
            {
                if (dest.pos >= 0) victim = dest.inv.GetItemAt(dest.pos);
            }
            catch
            {
            }

            if (dest.pos >= 0 && victim != null)
            {
                _cliSwapByVictim[victim] = (newItem, dest.inv, dest.pos, null);
                var srcLoot = dest.srcLoot ?? (LootView.Instance ? LootView.Instance.TargetInventory : null);
                Client_SendLootPutRequest(srcLoot, victim, dest.srcPos);
                return;
            }

            try
            {
                if (dest.pos >= 0 && dest.inv.AddAt(newItem, dest.pos)) return;
            }
            catch
            {
            }

            try
            {
                if (dest.inv.AddAndMerge(newItem, Mathf.Max(0, dest.pos))) return;
            }
            catch
            {
            }

            try
            {
                if (dest.inv.AddItem(newItem)) return;
            }
            catch
            {
            }

            
            PutBackToSource_NoTrack(newItem, dest);
            return;
        }

        
        var mc = LevelManager.Instance ? LevelManager.Instance.MainCharacter : null;
        var backpack = mc ? mc.CharacterItem != null ? mc.CharacterItem.Inventory : null : null;

        if (backpack != null)
        {
            try
            {
                if (backpack.AddAndMerge(newItem)) return;
            }
            catch
            {
            }

            try
            {
                if (backpack.AddItem(newItem)) return;
            }
            catch
            {
            }
        }

        
        PutBackToSource_NoTrack(newItem, dest);
    }

    public static void Client_ApplyLootVisibility(Dictionary<int, bool> vis)
    {
        try
        {
            var core = MultiSceneCore.Instance;
            if (core == null || vis == null) return;

            foreach (var kv in vis)
                core.inLevelData[kv.Key] = kv.Value; 

            
            var loaders = Object.FindObjectsOfType<LootBoxLoader>(true);
            foreach (var l in loaders)
                try
                {
                    var k = LootManager.Instance.ComputeLootKey(l.transform);
                    if (vis.TryGetValue(k, out var on))
                        l.gameObject.SetActive(on);
                }
                catch
                {
                }
        }
        catch
        {
        }
    }

    public void Server_HandleLootSlotPlugRequest(NetPeer peer, NetDataReader r)
    {
        
        var scene = r.GetInt();
        var posKey = r.GetInt();
        var iid = r.GetInt();
        var lootUid = r.GetInt();
        var inv = LootManager.Instance.ResolveLootInv(scene, posKey, iid, lootUid);
        if (inv == null || LootboxDetectUtil.IsPrivateInventory(inv))
        {
            Server_SendLootDeny(peer, "no_inv");
            return;
        }

        
        var master = LootManager.Instance.ReadItemRef(r, inv);
        var slotKey = r.GetString();
        if (!master)
        {
            Server_SendLootDeny(peer, "bad_weapon");
            Server_SendLootboxState(peer, inv);
            return;
        }

        var dstSlot = master?.Slots?.GetSlot(slotKey);
        if (dstSlot == null)
        {
            Server_SendLootDeny(peer, "bad_slot");
            Server_SendLootboxState(peer, inv);
            return;
        }

        
        var srcInLoot = r.GetBool();
        Item srcItem = null;
        uint token = 0;
        ItemSnapshot snap = default;

        if (srcInLoot)
        {
            srcItem = LootManager.Instance.ReadItemRef(r, inv);
            if (!srcItem)
            {
                Server_SendLootDeny(peer, "bad_src");
                Server_SendLootboxState(peer, inv); 
                return;
            }
        }
        else
        {
            token = r.GetUInt();
            snap = ItemTool.ReadItemSnapshot(r);
        }

        
        _serverApplyingLoot = true;
        var ok = false;
        Item unplugged = null;
        try
        {
            var child = srcItem;
            if (!srcInLoot)
            {
                
                child = ItemTool.BuildItemFromSnapshot(snap);
                if (!child)
                {
                    Server_SendLootDeny(peer, "build_fail");
                    Server_SendLootboxState(peer, inv);
                    return;
                }
            }
            else
            {
                
                try
                {
                    child.Detach();
                }
                catch
                {
                }
            }

            ok = dstSlot.Plug(child, out unplugged);

            if (ok)
            {
                
                if (!srcInLoot)
                {
                    var ack = new Net.HybridNet.LootPutOkMessage
                    {
                        Token = (int)token
                    };
                    Net.HybridNet.HybridNetCore.Send(ack, peer);
                }

                
                Server_SendLootboxState(null, inv);
            }
            else
            {
                Server_SendLootDeny(peer, "slot_plug_fail");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LOOT][PLUG] {ex}");
            ok = false;
        }
        finally
        {
            _serverApplyingLoot = false;
        }

        if (!ok)
        {
            
            if (!srcInLoot)
                try
                {
                    
                }
                catch
                {
                }

            Server_SendLootDeny(peer, "plug_fail");
            Server_SendLootboxState(peer, inv);
            return;
        }

        
        if (unplugged)
            if (!inv.AddAndMerge(unplugged))
                try
                {
                    if (unplugged) Object.Destroy(unplugged.gameObject);
                }
                catch
                {
                }

        
        if (!srcInLoot && token != 0)
        {
            var w2 = new Net.HybridNet.LootPutOkMessage
            {
                Token = (int)token
            };
            Net.HybridNet.HybridNetCore.Send(w2, peer);
        }

        
        Server_SendLootboxState(null, inv);
    }

    public void Client_RequestLootSlotPlug(Inventory inv, Item master, string slotKey, Item child)
    {
        if (!networkStarted || IsServer || connectedPeer == null) return;

        var w = new NetDataWriter();

        
        LootManager.Instance.PutLootId(w, inv);
        LootManager.Instance.WriteItemRef(w, inv, master);
        w.Put(slotKey);

        var srcInLoot = LootboxDetectUtil.IsLootboxInventory(child ? child.InInventory : null);
        w.Put(srcInLoot);

        if (srcInLoot)
        {
            
            LootManager.Instance.WriteItemRef(w, child.InInventory, child);
        }
        else
        {
            
            var token = ++_cliLocalToken; 
            _cliPendingSlotPlug[token] = child;
            w.Put(token);
            ItemTool.WriteItemSnapshot(w, child);
        }

        connectedPeer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    internal uint Client_RequestSlotUnplugToBackpack(Inventory lootInv, Item master, string slotKey, Inventory destInv, int destPos)
    {
        if (!networkStarted || IsServer || connectedPeer == null) return 0;
        if (!lootInv || !master || string.IsNullOrEmpty(slotKey)) return 0;
        if (!LootboxDetectUtil.IsLootboxInventory(lootInv) || LootboxDetectUtil.IsPrivateInventory(lootInv)) return 0;
        if (destInv && LootboxDetectUtil.IsLootboxInventory(destInv)) destInv = null; 

        
        var token = _nextLootToken++;
        if (destInv)
            LootManager.Instance._cliPendingTake[token] = new PendingTakeDest
            {
                inv = destInv,
                pos = destPos,
                slot = null,
                srcLoot = lootInv,
                srcPos = -1
            };

        
        Client_RequestLootSlotUnplug(lootInv, master, slotKey, true, token);
        return token;
    }

    internal void Client_RequestLootSlotUnplug(Inventory inv, Item master, string slotKey)
    {
        if (!networkStarted || IsServer || connectedPeer == null) return;
        if (!inv || !master || string.IsNullOrEmpty(slotKey)) return;

        var w = writer;
        if (w == null) return;
        w.Reset();
        LootManager.Instance.PutLootId(w, inv); 
        LootManager.Instance.WriteItemRef(w, inv, master); 
        w.Put(slotKey ?? string.Empty); 
        
        connectedPeer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    internal void Client_RequestLootSlotUnplug(Inventory inv, Item master, string slotKey, bool takeToBackpack, uint token)
    {
        if (!networkStarted || IsServer || connectedPeer == null) return;
        if (!inv || !master || string.IsNullOrEmpty(slotKey)) return;

        var w = writer;
        if (w == null) return;
        w.Reset();
        LootManager.Instance.PutLootId(w, inv); 
        LootManager.Instance.WriteItemRef(w, inv, master); 
        w.Put(slotKey ?? string.Empty); 

        w.Put(takeToBackpack);
        w.Put(token);
        connectedPeer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    public void Server_HandleLootSlotUnplugRequest(NetPeer peer, NetDataReader r)
    {
        
        var scene = r.GetInt();
        var posKey = r.GetInt();
        var iid = r.GetInt();
        var lootUid = r.GetInt();

        var inv = LootManager.Instance.ResolveLootInv(scene, posKey, iid, lootUid);
        if (inv == null || LootboxDetectUtil.IsPrivateInventory(inv))
        {
            Server_SendLootDeny(peer, "no_inv");
            return;
        }

        
        var master = LootManager.Instance.ReadItemRef(r, inv);
        var slotKey = r.GetString();
        if (!master)
        {
            Server_SendLootDeny(peer, "bad_weapon");
            return;
        }

        var slot = master?.Slots?.GetSlot(slotKey);
        if (slot == null)
        {
            Server_SendLootDeny(peer, "bad_slot");
            Server_SendLootboxState(peer, inv); 
            return;
        }

        
        var takeToBackpack = false;
        uint token = 0;
        if (r.AvailableBytes >= 5) 
            try
            {
                takeToBackpack = r.GetBool();
                token = r.GetUInt();
            }
            catch
            {
            }

        
        Item removed = null;
        var ok = false;
        _serverApplyingLoot = true; 
        try
        {
            removed = slot.Unplug();
            ok = removed != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LOOT][UNPLUG] {ex}");
            ok = false;
        }
        finally
        {
            _serverApplyingLoot = false;
        }

        if (!ok || !removed)
        {
            Server_SendLootDeny(peer, "slot_unplug_fail");
            Server_SendLootboxState(peer, inv); 
            return;
        }

        
        if (!takeToBackpack)
        {
            if (!inv.AddAndMerge(removed))
            {
                try
                {
                    if (removed) Object.Destroy(removed.gameObject);
                }
                catch
                {
                }

                Server_SendLootDeny(peer, "add_fail");
                Server_SendLootboxState(peer, inv);
                return;
            }

            Server_SendLootboxState(null, inv); 
            return;
        }

        
        var wCli = new Net.HybridNet.LootTakeOkMessage
        {
            Token = (int)token,
            ItemSnapshot = ""
        };
        Net.HybridNet.HybridNetCore.Send(wCli, peer);

        try
        {
            if (removed) Object.Destroy(removed.gameObject);
        }
        catch
        {
        }

        Server_SendLootboxState(null, inv);
    }

    public void Client_SendLootSplitRequest(Inventory lootInv, int srcPos, int count, int preferPos)
    {
        if (!networkStarted || IsServer || connectedPeer == null || lootInv == null) return;
        if (LootboxDetectUtil.IsPrivateInventory(lootInv)) return;
        if (count <= 0) return;

        var w = writer;
        if (w == null) return;
        w.Reset();
        LootManager.Instance.PutLootId(w, lootInv); 
        w.Put(srcPos);
        w.Put(count);
        w.Put(preferPos); 
        connectedPeer.Send(w, DeliveryMethod.ReliableOrdered);
    }

    public void Server_HandleLootSplitRequest(NetPeer peer, NetDataReader r)
    {
        var scene = r.GetInt();
        var posKey = r.GetInt();
        var iid = r.GetInt();
        var lootUid = r.GetInt();
        var srcPos = r.GetInt();
        var count = r.GetInt();
        var prefer = r.GetInt();

        
        Inventory inv = null;
        if (lootUid >= 0) LootManager.Instance._srvLootByUid.TryGetValue(lootUid, out inv);
        if (inv == null && !LootManager.Instance.TryResolveLootById(scene, posKey, iid, out inv))
        {
            Server_SendLootDeny(peer, "no_inv");
            return;
        }

        if (LootboxDetectUtil.IsPrivateInventory(inv))
        {
            Server_SendLootDeny(peer, "no_inv");
            return;
        }

        var srcItem = inv.GetItemAt(srcPos);
        if (!srcItem || count <= 0 || !srcItem.Stackable || count >= srcItem.StackCount)
        {
            Server_SendLootDeny(peer, "split_bad");
            return;
        }

        ItemTool.Server_DoSplitAsync(inv, srcPos, count, prefer).Forget();
    }


    public struct ItemSnapshot
    {
        public int typeId;
        public int stack;
        public float durability;
        public float durabilityLoss;
        public bool inspected;
        public List<(string key, ItemSnapshot child)> slots; 
        public List<ItemSnapshot> inventory; 
    }

    public struct PendingTakeDest
    {
        
        public Inventory inv;
        public int pos;
        public Slot slot;

        
        public Inventory srcLoot;
        public int srcPos;
    }
}