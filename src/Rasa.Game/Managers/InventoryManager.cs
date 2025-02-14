﻿using System;
using System.Collections.Generic;

namespace Rasa.Managers
{
    using Data;
    using Game;
    using Packets.Communicator.Server;
    using Packets.Inventory.Client;
    using Packets.Inventory.Server;
    using Packets.MapChannel.Client;
    using Packets.MapChannel.Server;
    using Repositories.UnitOfWork;
    using Structures;
    using Structures.Char;

    public class InventoryManager
    {
        /*    Inventory Packets:
         *      Done:
         *  - AddBuybackItem
         *  - InventoryAddItem
         *  - InventoryCreate
         *  - InventoryRemoveItem
         *  - LockboxTabPermissions
         *  - RemoveBuybackItem
         *  
         *      ToDo:
         *  - AddAuctionItem
         *  - AddInboxItem
         *  - AddOverflowItem
         *  - AddWagerItem
         *  - InventoryDestroy
         *  - InventoryMoveFailed
         *  - InventoryReload
         *  - RemoveAuctionItem
         *  - RemoveInboxItem
         *  - RemoveOverflowItem
         *  - RemoveWagerItem
         *  - ResetAuctionInventory
         *  - ResetBuybackInventory
         *  - ResetInboxInventory
         *  - ResetOverflowInventory
         *  - ResetWagerInventory
         *  
         *    Inventory Handlers:
         *  - ClanLockbox_DepositItemInSlot         => implemented
         *  - ClanLockbox_DepositItemInTab          => implemented
         *  - ClanLockbox_DestroyItem               => implemented
         *  - ClanLockbox_MoveItem                  => implemented
         *  - ClanLockbox_WithdrawItem              => implemented
         *  - HomeInventory_DestroyItem             => implemented
         *  - HomeInventory_MoveItem                => implemented
         *  - OverflowTransfer                      => ToDo
         *  - PersonalInventory_DestroyItem         => implemented
         *  - PersonalInventory_MoveItem            => implemented
         *  - PurchaseClanLockboxTab                => ToDo
         *  - PurchaseLockboxTab                    => implemented
         *  - RequestEquipArmor                     => implemented
         *  - RequestEquipWeapon                    => implemented
         *  - RequestLockboxTabPermissions          => implemented
         *  - RequestMoveItemToHomeInventory        => implemented
         *  - RequestTakeItemFromHomeInventory      => implemented
         *  - RequestTakeItemFromInboxInventory     => ToDo
         *  - TransferCreditToLockbox               => implemented
         *  - WeaponDrawerInventory_MoveItem        => implemented
         */

        private static InventoryManager _instance;
        private static readonly object InstanceLock = new object();
        private readonly IGameUnitOfWorkFactory _gameUnitOfWorkFactory;
        public static InventoryManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new InventoryManager(Server.GameUnitOfWorkFactory);
                    }
                }

                return _instance;
            }
        }

        private InventoryManager(IGameUnitOfWorkFactory gameUnitOfWorkFactory)
        {
            _gameUnitOfWorkFactory = gameUnitOfWorkFactory;
        }

        #region Handlers

        public void HomeInventory_DestroyItem(Client client, HomeInventory_DestroyItemPacket packet)
        {
            if (packet.EntityId == 0)
                return;

            var tempItem = EntityManager.Instance.GetItem(packet.EntityId);

            ReduceStackCount(client, InventoryType.HomeInventory, tempItem, packet.Quantity);

            // ToDo delete item from db? or we sill keep all items
        }

        public void HomeInventory_MoveItem(Client client, HomeInventory_MoveItemPacket packet)
        {
            // remove item
            if (packet.SrcSlot == packet.DestSlot)
                return;

            if (packet.SrcSlot < 0 || packet.SrcSlot >= 480)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot >= 480)
                return;

            var entityId = client.Player.Inventory.HomeInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            RemoveItemBySlot(client, InventoryType.HomeInventory, packet.SrcSlot);
            // if toSlot is not empty, move current item to SrcSlot (item swap)
            if (client.Player.Inventory.HomeInventory[(int)packet.DestSlot] != 0)
                AddItemBySlot(client, InventoryType.HomeInventory, client.Player.Inventory.HomeInventory[(int)packet.DestSlot], packet.SrcSlot, true);

            AddItemBySlot(client, InventoryType.HomeInventory, entityId, packet.DestSlot, true);
        }

        public void PersonalInventory_DestroyItem(Client client, PersonalInventory_DestroyItemPacket packet)
        {
            if (packet.EntityId == 0)
                return;

            var tempItem = EntityManager.Instance.GetItem(packet.EntityId);

            ReduceStackCount(client, InventoryType.Personal, tempItem, packet.Quantity);

            // ToDo delete item from db? or we sill keep all items
        }

        public void PersonalInventory_MoveItem(Client client, PersonalInventory_MoveItemPacket packet)
        {
            // remove item
            if (packet.SrcSlot == packet.DestSlot)
                return;

            if (packet.SrcSlot < 0 || packet.SrcSlot > 250)
            {
                Logger.WriteLog(LogType.Debug, $"SrcSlot out of range => {packet.SrcSlot}");
                return;
            }

            if (packet.DestSlot < 0 || packet.DestSlot > 250)
            {
                Logger.WriteLog(LogType.Debug, $"DestSlot out of range => {packet.DestSlot}");
                return;
            }

            var entityId = client.Player.Inventory.PersonalInventory[packet.SrcSlot];

            if (entityId == 0)
                return;

            RemoveItemBySlot(client, InventoryType.Personal, (uint)packet.SrcSlot);
            // if toSlot is not empty, move current item to SrcSlot (item swap)
            if (client.Player.Inventory.PersonalInventory[packet.DestSlot] != 0)
                AddItemBySlot(client, InventoryType.Personal, client.Player.Inventory.PersonalInventory[packet.DestSlot], (uint)packet.SrcSlot, true);

            AddItemBySlot(client, InventoryType.Personal, entityId, (uint)packet.DestSlot, true);
        }

        public void PurchaseLockboxTab(Client client, PurchaseLockboxTabPacket packet)
        {
            /* ToDo
             * player credits are checked on client side
             * should we add server side check too?
             */

            if (packet.TabId == 2)  // price is 100 000
                ManifestationManager.Instance.LossCredits(client, 100000);
            if (packet.TabId == 3)  // price is 1 000 000
                ManifestationManager.Instance.LossCredits(client, 1000000);
            if (packet.TabId == 4)  // price is 10 000 000
                ManifestationManager.Instance.LossCredits(client, 10000000);
            if (packet.TabId == 5)  // price is 100 000 000
                ManifestationManager.Instance.LossCredits(client, 100000000);

            // update Player
            client.Player.LockboxTabs = packet.TabId;
            // update Db
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            unitOfWork.CharacterLockboxes.UpdatePurashedTabs(client.AccountEntry.Id, packet.TabId);
            // send data to client
            client.CallMethod(SysEntity.ClientInventoryManagerId, new LockboxTabPermissionsPacket(packet.TabId));
        }

        public void RequestEquipArmor(Client client, RequestEquipArmorPacket packet)
        {
            if (packet.SrcInventory != InventoryType.Personal)
            {
                Logger.WriteLog(LogType.Debug, $"Unsupported inventory => {packet.SrcInventory}");
                return;
            }

            if (packet.SrcSlot < 0 || packet.SrcSlot >= 50)
            {
                Logger.WriteLog(LogType.Debug, $"SrcSlot out of range => {packet.SrcSlot}");
                return;
            }

            if (packet.DestSlot < 0 || packet.DestSlot > 22)
            {
                Logger.WriteLog(LogType.Debug, $"DestSlot out of range => {packet.DestSlot}");
                return;
            }

            var entityIdEquippedItem = client.Player.Inventory.EquippedInventory[(int)packet.DestSlot]; // the old equipped item (can be none)
            var entityIdInventoryItem = client.Player.Inventory.PersonalInventory[(int)packet.SrcSlot]; // the new equipped item (can be none)

            // can we equip the item
            var itemToEquip = EntityManager.Instance.GetItem(entityIdInventoryItem);
            var canEquip = ValidateItemEquip(client, itemToEquip);

            if (itemToEquip == null && canEquip == false)
                return;

            if (canEquip == false)
                return;

            // swap items on the client and server
            if (client.Player.Inventory.PersonalInventory[(int)packet.SrcSlot] != 0)
                RemoveItemBySlot(client, InventoryType.Personal, packet.SrcSlot);

            if (client.Player.Inventory.EquippedInventory[(int)packet.DestSlot] != 0)
                RemoveItemBySlot(client, InventoryType.EquipedInventory, packet.DestSlot);

            if (entityIdEquippedItem != 0)
                AddItemBySlot(client, InventoryType.Personal, entityIdEquippedItem, packet.SrcSlot, true);

            if (entityIdInventoryItem != 0)
                AddItemBySlot(client, InventoryType.EquipedInventory, entityIdInventoryItem, packet.DestSlot, true);

            // update appearance
            if (itemToEquip == null)
            {
                // remove item graphic if dequipped
                var prevEquippedItem = EntityManager.Instance.GetItem(entityIdEquippedItem);
                var equipableClassInfo = EntityClassManager.Instance.GetEquipableClassInfo(prevEquippedItem);
                ManifestationManager.Instance.RemoveAppearanceItem(client, equipableClassInfo.EquipmentSlotId);
            }
            else
                ManifestationManager.Instance.SetAppearanceItem(client, itemToEquip);

            ManifestationManager.Instance.UpdateAppearance(client);
            ManifestationManager.Instance.UpdateStatsValues(client, false);
            ManifestationManager.Instance.NotifyEquipmentUpdate(client);

            // Send Data to client
            client.CallMethod(client.Player.EntityId, new AttributeInfoPacket(client.Player.Attributes));
        }

        public void RequestEquipWeapon(Client client, RequestEquipWeaponPacket packet)
        {
            var srcSlot = packet.SrcSlot;
            var invType = packet.InventoryType;
            var destSlot = packet.DestSlot;

            if (invType != InventoryType.Personal)
            {
                Console.WriteLine("unsuported inventory");
                return;
            }

            if (srcSlot < 0 || srcSlot > 50)
                return;

            if (destSlot < 0 || destSlot > 5)
                return;

            // equip item
            var entityIdEquippedItem = client.Player.Inventory.WeaponDrawer[(int)destSlot]; // the old equipped item (can be none)
            var entityIdInventoryItem = client.Player.Inventory.PersonalInventory[(int)srcSlot]; // the new equipped item (can be none)

            // can we equip the item
            var itemToEquip = EntityManager.Instance.GetItem(entityIdInventoryItem);
            var canEquip = ValidateItemEquip(client, itemToEquip);

            if (itemToEquip == null && canEquip == false)
                return;

            if (canEquip == false)
                return;

            // swap items on the client and server
            if (client.Player.Inventory.PersonalInventory[(int)srcSlot] != 0)
                RemoveItemBySlot(client, InventoryType.Personal, srcSlot);
            if (client.Player.Inventory.WeaponDrawer[(int)destSlot] != 0)
                RemoveItemBySlot(client, InventoryType.WeaponDrawerInventory, destSlot);
            if (entityIdEquippedItem != 0)
                AddItemBySlot(client, InventoryType.Personal, entityIdEquippedItem, srcSlot, true);
            if (entityIdInventoryItem != 0)
                AddItemBySlot(client, InventoryType.WeaponDrawerInventory, entityIdInventoryItem, destSlot, true);

            if (destSlot == client.Player.ActiveWeapon)
                if (itemToEquip == null)
                {
                    // remove item graphic if dequipped
                    var prevEquippedItem = EntityManager.Instance.GetItem(entityIdEquippedItem);
                    var equipableClassInfo = EntityClassManager.Instance.GetEquipableClassInfo(prevEquippedItem);

                    RemoveItemBySlot(client, InventoryType.EquipedInventory, 13);
                    ManifestationManager.Instance.RemoveAppearanceItem(client, equipableClassInfo.EquipmentSlotId);

                    // we dont have weapon, set weaponReady to false
                    if (client.Player.WeaponReady)
                        ManifestationManager.Instance.WeaponReady(client, false);
                }
                else
                    ManifestationManager.Instance.SetAppearanceItem(client, itemToEquip);

            // Tell client that he have new weapon
            ManifestationManager.Instance.NotifyEquipmentUpdate(client);

            ManifestationManager.Instance.UpdateAppearance(client);
        }

        public void RequestLockboxTabPermissions(Client client)
        {
            client.CallMethod(SysEntity.ClientInventoryManagerId, new LockboxTabPermissionsPacket(client.Player.LockboxTabs));
        }

        public void RequestMoveItemToClanLockbox(Client client, RequestMoveItemToClanLockboxPacket packet)
        {
            Logger.WriteLog(LogType.Debug, $"ToDO: RequestMoveItemToClanLockboxPacket");
        }

        public void RequestMoveItemToHomeInventory(Client client, RequestMoveItemToHomeInventoryPacket packet)
        {
            // remove item
            if (packet.SrcSlot < 0 || packet.SrcSlot >= 250)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot >= 480)
                return;

            var entityId = client.Player.Inventory.PersonalInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            RemoveItemBySlot(client, InventoryType.Personal, packet.SrcSlot);
            // if toSlot is not empty, move current item to SrcSlot (item swap)
            if (client.Player.Inventory.HomeInventory[(int)packet.DestSlot] != 0)
                AddItemBySlot(client, InventoryType.Personal, client.Player.Inventory.HomeInventory[(int)packet.DestSlot], packet.SrcSlot, true);

            AddItemBySlot(client, InventoryType.HomeInventory, entityId, packet.DestSlot, true);
        }

        public void ClanLockbox_DepositItemInSlot(Client client, ClanLockbox_DepositItemInSlotPacket packet)
        {
            if (client.Player.ClanId == 0)
                return;

            if (packet.SrcSlot < 0 || packet.SrcSlot >= 250)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot >= 500)
                return;

            var entityId = client.Player.Inventory.PersonalInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            RemoveItemBySlot(client, InventoryType.Personal, packet.SrcSlot);

            // If DestSlot is not empty, move current item to SrcSlot (item swap)
            bool wasSwap = client.Player.Inventory.ClanInventory[(int)packet.DestSlot] != 0;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            if (wasSwap)
            {
                unitOfWork.CharacterInventories.DeleteInvItem(client.AccountEntry.Id, client.Player.Id, (uint)InventoryType.Personal, packet.SrcSlot);
                AddItemBySlot(client, InventoryType.Personal, client.Player.Inventory.ClanInventory[(int)packet.DestSlot], packet.SrcSlot, true, true);

                RemoveItemBySlotForClan(client.Player.ClanId, packet.DestSlot, 0);
                unitOfWork.ClanInventories.DeleteInvItem(client.Player.ClanId, packet.DestSlot);
            }

            AddItemBySlot(client, InventoryType.ClanInventory, entityId, packet.DestSlot, true, true);

            if (!wasSwap)
                unitOfWork.CharacterInventories.DeleteInvItem(client.AccountEntry.Id, client.Player.Id, (uint)InventoryType.Personal, packet.SrcSlot);

            EntityManager.Instance.GetItem(entityId).OwnerSlotId = packet.DestSlot;
            RefreshClanLockbox(client.Player.ClanId, entityId, client.Player.Id, packet.DestSlot, ref client.Player.Inventory.ClanInventory, true);
        }

        public void ClanLockbox_DepositItemInTab(Client client, ClanLockbox_DepositItemInTabPacket packet)
        {
            if (client.Player.ClanId == 0)
                return;

            if (packet.SrcSlot < 0 || packet.SrcSlot > 250)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot > 500)
                return;

            var entityId = client.Player.Inventory.PersonalInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            var tempItem = EntityManager.Instance.GetItem(entityId);

            RemoveItemBySlot(client, InventoryType.Personal, (uint)packet.SrcSlot);

            Item item = AddItemToClanInventory(client, tempItem);
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            unitOfWork.Items.UpdateItemStackSize(tempItem);
            if (item == null)
            {
                client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmInventoryFull, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                return;
            }

            unitOfWork.CharacterInventories.DeleteInvItem(client.AccountEntry.Id, client.Player.Id, (uint)InventoryType.Personal, (uint)packet.SrcSlot);

            if (EntityManager.Instance.GetItem(entityId) == null)
                return;

            RefreshClanLockbox(client.Player.ClanId, entityId, client.Player.Id, item.OwnerSlotId, ref client.Player.Inventory.ClanInventory, true);
        }

        public void ClanLockbox_MoveItem(Client client, ClanLockbox_MoveItemPacket packet)
        {
            if (client.Player.ClanId == 0)
                return;

            if (packet.SrcSlot == packet.DestSlot)
                return;

            if (packet.SrcSlot < 0 || packet.SrcSlot >= 500)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot >= 500)
                return;

            var entityId = client.Player.Inventory.ClanInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            // If DestSlot is not empty, move current item to SrcSlot (item swap)
            if (client.Player.Inventory.ClanInventory[(int)packet.DestSlot] != 0)
            {
                // Todo swap items
                return;
            }
            RemoveItemBySlot(client, InventoryType.ClanInventory, packet.SrcSlot); // Put this above swap if check once swap is implemented

            EntityManager.Instance.GetItem(entityId).OwnerSlotId = packet.DestSlot;
            AddItemBySlot(client, InventoryType.ClanInventory, entityId, packet.DestSlot, true, false);

            RemoveItemBySlotForClan(client.Player.ClanId, packet.SrcSlot, client.Player.Id);
            RefreshClanLockbox(client.Player.ClanId, entityId, client.Player.Id, packet.DestSlot, ref client.Player.Inventory.ClanInventory, true);
        }

        public void ClanLockbox_WithdrawItem(Client client, ClanLockbox_WithdrawItemPacket packet)
        {
            if (client.Player.ClanId == 0)
                return;

            // Only the leader and the rank below them can withdraw items from the clan lockbox.
            ClanMemberEntry member = ClanManager.Instance.GetClanMember(client.Player.ClanId, client.Player.Id);
            if (member.Rank < 2)
            {
                client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmClanInsufficientPermissions, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                return;
            }

            if (packet.SrcSlot < 0 || packet.SrcSlot > 500)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot > 250)
                return;

            var entityId = client.Player.Inventory.ClanInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            var tempItem = EntityManager.Instance.GetItem(entityId);
            bool wasSwap = client.Player.Inventory.PersonalInventory[(int)packet.DestSlot] != 0;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            if (packet.ManagePersonalSlot)
            {
                wasSwap = false;
                Item item = AddItemToInventory(client, tempItem);
                unitOfWork.Items.UpdateItemStackSize(tempItem);
                if (item == null)
                {
                    RefreshClanLockbox(client.Player.ClanId, entityId, client.Player.Id, 0, ref client.Player.Inventory.ClanInventory, false);
                    client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmInventoryFull, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                    return;
                }
            }
            else
            {
                if (wasSwap)
                {
                    RemoveItemBySlot(client, InventoryType.ClanInventory, packet.SrcSlot);
                    unitOfWork.ClanInventories.DeleteInvItem(client.Player.ClanId, packet.SrcSlot);
                    AddItemBySlot(client, InventoryType.ClanInventory, client.Player.Inventory.PersonalInventory[(int)packet.DestSlot], packet.SrcSlot, true, true);

                    var newEntityId = client.Player.Inventory.ClanInventory[(int)packet.SrcSlot];
                    RefreshClanLockbox(client.Player.ClanId, newEntityId, client.Player.Id, packet.SrcSlot, ref client.Player.Inventory.ClanInventory, true);

                    RemoveItemBySlot(client, InventoryType.Personal, packet.DestSlot);
                    unitOfWork.CharacterInventories.DeleteInvItem(client.AccountEntry.Id, client.Player.Id, (uint)InventoryType.Personal, packet.DestSlot);
                }
                AddItemBySlot(client, InventoryType.Personal, entityId, packet.DestSlot, true, true);
            }

            if (!wasSwap)
            {
                RemoveItemBySlotForClan(client.Player.ClanId, packet.SrcSlot, 0);
                unitOfWork.ClanInventories.DeleteInvItem(client.Player.ClanId, packet.SrcSlot);

                RefreshClanLockbox(client.Player.ClanId, entityId, client.Player.Id, 0, ref client.Player.Inventory.ClanInventory, false);
            }
        }

        public void ClanLockbox_DestroyItem(Client client, ClanLockbox_DestroyItemPacket packet)
        {
            if (client.Player.ClanId == 0)
                return;

            if (packet.EntityId == 0)
                return;

            var tempItem = EntityManager.Instance.GetItem(packet.EntityId);

            //TODO: Support deleting portions
            if ((tempItem.StackSize - packet.Quantity) > 0)
                return;

            RemoveItemBySlotForClan(client.Player.ClanId, tempItem.OwnerSlotId, 0);

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            unitOfWork.ClanInventories.DeleteInvItem(client.Player.ClanId, tempItem.OwnerSlotId);

            RefreshClanLockbox(client.Player.ClanId, packet.EntityId, client.Player.Id, 0, ref client.Player.Inventory.ClanInventory, false);
        }

        public void RequestTakeItemFromHomeInventory(Client client, RequestTakeItemFromHomeInventoryPacket packet)
        {
            // remove item
            if (packet.SrcSlot < 0 || packet.SrcSlot > 480)
                return;

            if (packet.DestSlot < 0 || packet.DestSlot > 250)
                return;

            var entityId = client.Player.Inventory.HomeInventory[(int)packet.SrcSlot];

            if (entityId == 0)
                return;

            RemoveItemBySlot(client, InventoryType.HomeInventory, packet.SrcSlot);
            // if toSlot is not empty, move current item to SrcSlot (item swap)
            if (client.Player.Inventory.PersonalInventory[(int)packet.DestSlot] != 0)
                AddItemBySlot(client, InventoryType.HomeInventory, client.Player.Inventory.PersonalInventory[(int)packet.DestSlot], packet.SrcSlot, true);

            AddItemBySlot(client, InventoryType.Personal, entityId, packet.DestSlot, true);
        }

        public void TransferCreditToLockbox(Client client, int amount)
        {
            /*
             * ToDo:
             * there is some bug with withdraw if withdraw value is less then 256
             * client send positive value, insted of negative one
             * so we will set min transfer value to 500 for now
             * we can take closer look at this later
             */

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            //deposit
            if (amount >= 500)
            {
                if (client.Player.Credits[CurencyType.Credits] >= amount)
                {
                    var deposit = client.Player.LockboxCredits + amount;

                    ManifestationManager.Instance.LossCredits(client, -amount);

                    client.CallMethod(client.Player.EntityId, new LockboxFundsPacket(deposit));

                    client.Player.LockboxCredits = deposit;
                    unitOfWork.CharacterLockboxes.UpdateCredits(client.AccountEntry.Id, deposit);
                }
                else
                    CommunicatorManager.Instance.SystemMessage(client, "Not enof credit's in inventory\nP.S. Go earn some credits :)");
            }
            // withdraw
            else if (amount <= -500)
            {
                if (client.Player.LockboxCredits >= -amount)
                {
                    var withdraw = client.Player.LockboxCredits + amount;

                    ManifestationManager.Instance.GainCredits(client, -amount);
                    client.CallMethod(client.Player.EntityId, new LockboxFundsPacket(withdraw));

                    client.Player.LockboxCredits = withdraw;
                    unitOfWork.CharacterLockboxes.UpdateCredits(client.AccountEntry.Id, withdraw);
                }
                else
                    CommunicatorManager.Instance.SystemMessage(client, "Not enof credit's in Lockbox\nP.S. Dont be greedy :)");
            }
            else
                CommunicatorManager.Instance.SystemMessage(client, "Minimum transfer value is 500 credits");

        }

        public void ClanCreditTransfer(Client client, long amount, uint creditType)
        {
            if (client.Player.ClanId == 0)
                return;

            //-amount means withdraw from lockbox +amount means deposit to lockbox

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var clanInfo = unitOfWork.Clans.GetClanById(client.Player.ClanId);

            long remainderOfCredits = (creditType == 1 ? clanInfo.Credits : clanInfo.Prestige) + amount;

            if (amount >= 500 || amount <= -500)
            {
                if ((creditType == 1 && client.Player.Credits[CurencyType.Credits] < amount) ||
                    (creditType == 2 && client.Player.Credits[CurencyType.Prestige] < amount))
                {
                    if (amount > 0)
                        client.CallMethod(SysEntity.CommunicatorId, new DisplayClientMessagePacket(PlayerMessage.PmInsufficientDepositFunds, new Dictionary<string, string>(), MsgFilterId.GeneralSystemMessages));
                    return;
                }

                if (remainderOfCredits >= 0)
                {
                    if (creditType == 1)
                        unitOfWork.Clans.UpdateCredits(client.Player.ClanId, (uint)remainderOfCredits);
                    else
                        unitOfWork.Clans.UpdatePrestige(client.Player.ClanId, (uint)remainderOfCredits);

                    CharacterManager.Instance.UpdateCharacter(client, CharacterUpdate.Credits, amount * -1);
                    var augmentationsList = EntityClassManager.Instance.LoadedEntityClasses[EntityClasses.UsableClanLockboxV01].Augmentations;

                    foreach (var dynamicObj in EntityManager.Instance.DynamicObjects)
                    {
                        DynamicObject dynamicObject = dynamicObj.Value;

                        if (dynamicObject.EntityClassId == EntityClasses.UsableClanLockboxV01)
                            ClanManager.Instance.CallMethodForOnlineMembers(client.Player.ClanId, dynamicObject.EntityId, new UpdateClanLockboxCreditsPacket(creditType == 1 ? (uint)remainderOfCredits : clanInfo.Credits, creditType == 2 ? (uint)remainderOfCredits : clanInfo.Prestige));
                    }
                }
                else
                    CommunicatorManager.Instance.SystemMessage(client, "Not enough credit's");
            }
            else
                CommunicatorManager.Instance.SystemMessage(client, "Minimum transfer value is 500 credits");
        }

        public void WeaponDrawerInventory_MoveItem(Client client, WeaponDrawerInventory_MoveItemPacket packet)
        {
            var srcEntityId = client.Player.Inventory.WeaponDrawer[(int)packet.SrcSlot];
            var destEntityId = client.Player.Inventory.WeaponDrawer[(int)packet.DestSlot];
            // swap items on the client and server
            if (destEntityId != 0)
            {
                RemoveItemBySlot(client, InventoryType.WeaponDrawerInventory, packet.SrcSlot);
                RemoveItemBySlot(client, InventoryType.WeaponDrawerInventory, packet.DestSlot);
                AddItemBySlot(client, InventoryType.WeaponDrawerInventory, srcEntityId, packet.DestSlot, true);
                AddItemBySlot(client, InventoryType.WeaponDrawerInventory, destEntityId, packet.SrcSlot, true);
            }
            else
            {
                RemoveItemBySlot(client, InventoryType.WeaponDrawerInventory, packet.SrcSlot);
                AddItemBySlot(client, InventoryType.WeaponDrawerInventory, srcEntityId, packet.DestSlot, true);
            }
        }

        #endregion

        #region Helper Functions

        public void UpdateItemSlot(Client client, ulong entityId)
        {
            Item tempItem = EntityManager.Instance.GetItem(entityId);
            ItemManager.Instance.SendItemDataToClient(client, tempItem, false);
        }

        public void AddItemBySlot(Client client, InventoryType inventoryType, ulong entityId, uint slotId, bool updateDB, bool actuallyAdd = false)
        {
            var tempItem = EntityManager.Instance.GetItem(entityId);

            if (tempItem == null)
                return;

            // set entityId in slot
            switch (inventoryType)
            {
                case InventoryType.Personal:
                    client.Player.Inventory.PersonalInventory[(int)slotId] = tempItem.EntityId; // update slot
                    break;
                case InventoryType.HomeInventory:
                    client.Player.Inventory.HomeInventory[(int)slotId] = tempItem.EntityId; // update slot
                    break;
                case InventoryType.EquipedInventory:
                    client.Player.Inventory.EquippedInventory[(int)slotId] = tempItem.EntityId; // update slot
                    break;
                case InventoryType.WeaponDrawerInventory:
                    client.Player.Inventory.EquippedInventory[13] = tempItem.EntityId; // update slot
                    client.Player.Inventory.WeaponDrawer[(int)slotId] = tempItem.EntityId; // update slot
                    break;
                case InventoryType.ClanInventory:
                    client.Player.Inventory.ClanInventory[(int)slotId] = tempItem.EntityId; // update slot
                    break;
                default:
                    Console.WriteLine("Unsuported inventory type");
                    break;
            }
            // send inventoryAddItem
            client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryAddItemPacket(inventoryType, tempItem.EntityId, slotId));
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            if (inventoryType == InventoryType.HomeInventory)
                tempItem.OwnerId = 0;

            // update item in database
            if (updateDB)
            {
                if (inventoryType == InventoryType.ClanInventory)
                {
                    if (actuallyAdd)
                    {
                        unitOfWork.ClanInventories.AddInvItem(client.Player.ClanId, slotId, tempItem.Id);
                    }
                    else
                    {
                        unitOfWork.ClanInventories.MoveInvItem(client.Player.ClanId, slotId, tempItem.Id);
                    }
                }
                else
                {
                    if (actuallyAdd)
                    {
                        unitOfWork.CharacterInventories.AddInvItem(client.AccountEntry.Id, tempItem.OwnerId, (uint)inventoryType, slotId, tempItem.Id);
                    }
                    else
                    {
                        unitOfWork.CharacterInventories.MoveInvItem(client.AccountEntry.Id, tempItem.OwnerId, (uint)inventoryType, slotId, tempItem.Id);
                    }
                }
            }
        }

        public Item AddItemToInventory(Client client, Item item)
        {
            if (item == null)
                return null;

            var itemClassInfo = EntityClassManager.Instance.GetItemClassInfo(item);

            // get item category offset
            var itemCategoryOffset = (int)item.ItemTemplate.InventoryCategory - 1;

            if (itemCategoryOffset < 0 || itemCategoryOffset >= 5)
            {
                Logger.WriteLog(LogType.Error, $"AddItemToInventory: ItemTemplateId = {item.ItemTemplate.ItemTemplateId} inventory category = {item.ItemTemplate.InventoryCategory} is invalid");
                return null;
            }

            itemCategoryOffset *= 50;
            var stackSizeChanged = false;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            // see if we can merge the item into an already existing item
            for (var i = 0; i < 50; i++)
                if (client.Player.Inventory.PersonalInventory[itemCategoryOffset + i] != 0)
                {
                    // get item
                    var slotItem = EntityManager.Instance.GetItem(client.Player.Inventory.PersonalInventory[itemCategoryOffset + i]);

                    // same item template?
                    if (slotItem.ItemTemplate.ItemTemplateId != item.ItemTemplate.ItemTemplateId)
                        continue;

                    // calculate how many items we can add to the stack
                    var stackAdd = itemClassInfo.StackSize - slotItem.StackSize;
                    if (stackAdd == 0)
                        continue;

                    // add item to existing stack
                    var stackMove = Math.Min(stackAdd, item.StackSize);
                    slotItem.StackSize += stackMove;
                    unitOfWork.Items.UpdateItemStackSize(slotItem);

                    // remove stack's from source item
                    item.StackSize -= stackMove;
                    stackSizeChanged = true;

                    // notify client of changed stack count
                    client.CallMethod(slotItem.EntityId, new SetStackCountPacket(slotItem.StackSize));

                    if (item.StackSize == 0)
                    {
                        // destroy the item
                        EntityManager.Instance.DestroyPhysicalEntity(client, item.EntityId, EntityType.Item);
                        // remove from DB
                        unitOfWork.Items.DeleteItem(item.Id);
                        // return the 'new' item instead
                        return slotItem;
                    }

                }

            // item have new stackSize?
            if (stackSizeChanged)
                client.CallMethod(item.EntityId, new SetStackCountPacket(item.StackSize));

            // find free slot
            for (var i = 0; i < 50; i++)
            {
                if (client.Player.Inventory.PersonalInventory[itemCategoryOffset + i] == 0)
                {
                    item.OwnerId = client.Player.Id;
                    item.OwnerSlotId = (uint)(itemCategoryOffset + i);
                    item.CurrentHitPoints = itemClassInfo.MaxHitPoints;
                    // send data to client
                    ItemManager.Instance.SendItemDataToClient(client, item, false);
                    // add item to empty slot
                    AddItemBySlot(client, InventoryType.Personal, item.EntityId, (uint)(itemCategoryOffset + i), true, true);
                    return item;
                }
            }

            return null;
        }

        public Item AddItemToClanInventory(Client client, Item item)
        {
            if (item == null)
                return null;

            var itemClassInfo = EntityClassManager.Instance.GetItemClassInfo(item);
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var stackSizeChanged = false;
            // see if we can merge the item into an already existing item
            for (var i = 0; i < 500; i++)
                if (client.Player.Inventory.ClanInventory[i] != 0)
                {
                    // get item
                    var slotItem = EntityManager.Instance.GetItem(client.Player.Inventory.ClanInventory[i]);

                    // same item template?
                    if (slotItem.ItemTemplate.ItemTemplateId != item.ItemTemplate.ItemTemplateId)
                        continue;

                    // calculate how many items we can add to the stack
                    var stackAdd = itemClassInfo.StackSize - slotItem.StackSize;
                    if (stackAdd == 0)
                        continue;

                    // add item to existing stack
                    var stackMove = Math.Min(stackAdd, item.StackSize);
                    slotItem.StackSize += stackMove;
                    unitOfWork.Items.UpdateItemStackSize(slotItem);

                    // remove stack's from source item
                    item.StackSize -= stackMove;
                    stackSizeChanged = true;

                    // notify client of changed stack count
                    //client.CallMethod(slotItem.EntityId, new SetStackCountPacket(slotItem.Stacksize));
                    ClanManager.Instance.CallMethodForOnlineMembers(client.Player.ClanId, slotItem.EntityId, new SetStackCountPacket(slotItem.StackSize));

                    if (item.StackSize == 0)
                    {
                        // destroy the item
                        EntityManager.Instance.DestroyPhysicalEntity(client, item.EntityId, EntityType.Item);
                        // remove from DB
                        unitOfWork.Items.DeleteItem(item.Id);
                        // return the 'new' item instead
                        return slotItem;
                    }

                }

            // item have new stackSize?
            if (stackSizeChanged)
                client.CallMethod(item.EntityId, new SetStackCountPacket(item.StackSize));

            // find free slot
            for (var i = 0; i < 500; i++)
            {
                if (client.Player.Inventory.ClanInventory[i] == 0)
                {
                    item.OwnerId = client.AccountEntry.SelectedSlot;
                    item.OwnerSlotId = (uint)(i);
                    item.CurrentHitPoints = itemClassInfo.MaxHitPoints;
                    // send data to client
                    ItemManager.Instance.SendItemDataToClient(client, item, false);
                    // add item to empty slot
                    AddItemBySlot(client, InventoryType.ClanInventory, item.EntityId, (uint)(i), true, true);
                    return item;
                }
            }

            return null;
        }

        public Item CurrentWeapon(Client client)
        {
            return EntityManager.Instance.GetItem(client.Player.Inventory.EquippedInventory[13]);
        }

        public uint FreeSlotIndex(Manifestation player, InventoryType inventoryType, uint slotIndex)
        {
            switch (inventoryType)
            {
                case InventoryType.Personal:
                    player.Inventory.PersonalInventory[(int)slotIndex] = 0; // update slot
                    break;
                case InventoryType.HomeInventory:
                    player.Inventory.HomeInventory[(int)slotIndex] = 0; // update slot
                    break;
                case InventoryType.EquipedInventory:
                    player.Inventory.EquippedInventory[(int)slotIndex] = 0; // update slot
                    break;
                case InventoryType.WeaponDrawerInventory:
                    player.Inventory.WeaponDrawer[(int)slotIndex] = 0;    // update slot
                    break;
                default:
                    Console.WriteLine("RemoveItemBySlot: Invalid inventoryType{0}/slotIndex{1}\n", inventoryType, slotIndex);
                    break;
            }

            return slotIndex;
        }

        public void InitForClient(Client client)
        {
            InitCharacterInventory(client);

            // init LockboxTabPermissions
            client.CallMethod(SysEntity.ClientInventoryManagerId, new LockboxTabPermissionsPacket(client.Player.LockboxTabs));

            // it seems  that InventoryCreatePacket dont need to be called, ToDo; investigate more
            //client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryCreatePacket(InventoryType.Personal, client.MapClient.Inventory.PersonalInventory, 250));
            //client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryCreatePacket(InventoryType.HomeInventory, client.MapClient.Inventory.HomeInventory, 480));
            //client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryCreatePacket(InventoryType.WeaponDrawerInventory, client.MapClient.Inventory.WeaponDrawer, 5));
            //client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryCreatePacket(InventoryType.EquipedInventory, client.MapClient.Inventory.EquippedInventory, 22));
        }

        public void SetupLocalClanInventory(Client client)
        {
            if (client.Player.ClanId == 0)
                return;

            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();

            List<ClanInventoryEntry> getClanInventoryData = unitOfWork.ClanInventories.GetItems(client.Player.ClanId);

            foreach (var item in getClanInventoryData)
            {
                var itemData = unitOfWork.Items.GetItem(item.ItemId);
                var itemTemplate = ItemManager.Instance.GetItemTemplateById(itemData.ItemTemplateId);

                if (itemTemplate == null)
                    return;

                Item tempItem = null;

                foreach (var entities in EntityManager.Instance.Items)
                {
                    Item existingItem = entities.Value;

                    if (existingItem.Id == item.ItemId)
                    {
                        tempItem = existingItem;
                    }
                }

                // check if item is weapon
                if (tempItem.ItemTemplate.WeaponInfo != null)
                    tempItem.CurrentAmmo = itemData.AmmoCount;

                // fill invenoty slot
                ItemManager.Instance.SendItemDataToClient(client, tempItem, false);

                AddItemBySlot(client, InventoryType.ClanInventory, tempItem.EntityId, tempItem.OwnerSlotId, false);
            }

            client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryCreatePacket(InventoryType.ClanInventory, client.Player.Inventory.ClanInventory, 500));
        }

        public void InitClanInventory(Client client)
        {
            for (uint i = 0; i < 500; i++)
                client.Player.Inventory.ClanInventory.Add(0);

            SetupLocalClanInventory(client);
        }

        public void InitCharacterInventory(Client client)
        {
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            var getInventoryData = unitOfWork.CharacterInventories.GetItems(client.AccountEntry.Id);

            // init for server inventory
            for (uint i = 0; i < 22; i++)
                client.Player.Inventory.EquippedInventory.Add(0);

            for (uint i = 0; i < 480; i++)
                client.Player.Inventory.HomeInventory.Add(0);

            for (uint i = 0; i < 250; i++)
                client.Player.Inventory.PersonalInventory.Add(0);

            for (uint i = 0; i < 5; i++)
                client.Player.Inventory.WeaponDrawer.Add(0);

            foreach (var item in getInventoryData)
            {
                var itemData = unitOfWork.Items.GetItem(item.ItemId);
                var itemTemplate = ItemManager.Instance.GetItemTemplateById(itemData.ItemTemplateId);

                if (itemTemplate == null)
                    return;

                var newItem = new Item
                {
                    OwnerId = item.CharacterId,
                    OwnerSlotId = item.SlotId,
                    ItemTemplate = itemTemplate,
                    StackSize = itemData.StackSize,
                    CurrentHitPoints = itemData.CurrentHitPoints,
                    Color = itemData.Color,
                    Id = item.ItemId,
                    Crafter = itemData.CrafterName
                };

                // check if item is weapon
                if (newItem.ItemTemplate.WeaponInfo != null)
                    newItem.CurrentAmmo = itemData.AmmoCount;

                // register item
                EntityManager.Instance.RegisterEntity(newItem.EntityId, EntityType.Item);
                EntityManager.Instance.RegisterItem(newItem.EntityId, newItem);

                // fill invenoty slot
                ItemManager.Instance.SendItemDataToClient(client, newItem, false);

                if (item.CharacterId == client.Player.Id)
                {
                    if ((InventoryType)item.InventoryType == InventoryType.Personal)
                        AddItemBySlot(client, InventoryType.Personal, newItem.EntityId, newItem.OwnerSlotId, false);

                    else if ((InventoryType)item.InventoryType == InventoryType.EquipedInventory)
                        AddItemBySlot(client, InventoryType.EquipedInventory, newItem.EntityId, newItem.OwnerSlotId, false);

                    else if ((InventoryType)item.InventoryType == InventoryType.WeaponDrawerInventory)
                    {
                        AddItemBySlot(client, InventoryType.WeaponDrawerInventory, newItem.EntityId, newItem.OwnerSlotId, false);

                        if (newItem.OwnerSlotId == client.Player.ActiveWeapon)
                            client.Player.Inventory.EquippedInventory[13] = newItem.EntityId;
                    }
                }
                else if (item.CharacterId == 0)
                {
                    if ((InventoryType)item.InventoryType == InventoryType.HomeInventory)
                    {
                        client.Player.Inventory.HomeInventory[(int)item.SlotId] = newItem.EntityId;
                        // make the item appear on the client
                        AddItemBySlot(client, InventoryType.HomeInventory, client.Player.Inventory.HomeInventory[(int)item.SlotId], item.SlotId, false);
                    }
                }

            }
        }

        public void ReduceStackCount(Client client, InventoryType inventoryType, Item tempItem, uint stackDecreaseCount)
        {
            if (tempItem.OwnerId != client.AccountEntry.SelectedSlot)
                return; // item is not on this client's inventory

            var newStackCount = tempItem.StackSize - stackDecreaseCount;
            using var unitOfWork = _gameUnitOfWorkFactory.CreateChar();
            if (newStackCount <= 0)
            {

                // destroy item
                EntityManager.Instance.DestroyPhysicalEntity(client, tempItem.EntityId, EntityType.Item);
                client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryRemoveItemPacket(InventoryType.Personal, tempItem.EntityId));
                // free slot
                FreeSlotIndex(client.Player, inventoryType, tempItem.OwnerSlotId);
                // AddOrUpdate db
                var characterSlot = client.AccountEntry.SelectedSlot;

                if (inventoryType == InventoryType.HomeInventory)
                    characterSlot = 0;

                unitOfWork.CharacterInventories.DeleteInvItem(client.AccountEntry.Id, client.Player.Id, (uint)InventoryType.Personal, tempItem.OwnerSlotId);
                // ToDo will we delete items from db, or we will let tham stay, so thay can be retrived
                //ItemsTable.DeleteItem(tempItem.ItemId);
            }
            else
            {
                // update stack count
                tempItem.StackSize = newStackCount;
                // set stackcount
                client.CallMethod(tempItem.EntityId, new SetStackCountPacket(newStackCount));
                // update stack count in database
                unitOfWork.Items.UpdateItemStackSize(tempItem);
            }
        }

        public void RemoveItemBySlot(Client client, InventoryType inventoryType, uint slotIndex)
        {
            var entityId = 0ul;

            switch (inventoryType)
            {
                case InventoryType.Personal:
                    entityId = client.Player.Inventory.PersonalInventory[(int)slotIndex];
                    client.Player.Inventory.PersonalInventory[(int)slotIndex] = 0;
                    break;
                case InventoryType.HomeInventory:
                    entityId = client.Player.Inventory.HomeInventory[(int)slotIndex];
                    client.Player.Inventory.HomeInventory[(int)slotIndex] = 0;
                    break;
                case InventoryType.EquipedInventory:
                    entityId = client.Player.Inventory.EquippedInventory[(int)slotIndex];
                    client.Player.Inventory.EquippedInventory[(int)slotIndex] = 0;
                    break;
                case InventoryType.WeaponDrawerInventory:
                    entityId = client.Player.Inventory.WeaponDrawer[(int)slotIndex];
                    client.Player.Inventory.WeaponDrawer[(int)slotIndex] = 0;
                    break;
                case InventoryType.ClanInventory:
                    entityId = client.Player.Inventory.ClanInventory[(int)slotIndex];
                    client.Player.Inventory.ClanInventory[(int)slotIndex] = 0;
                    break;
                default:
                    Logger.WriteLog(LogType.Error, $"RemoveItemBySlot: Unsuported Inventory type {inventoryType}");
                    return;
            }

            client.CallMethod(SysEntity.ClientInventoryManagerId, new InventoryRemoveItemPacket(inventoryType, entityId));
        }

        public void RequestTooltipForItemTemplateId(Client client, uint itemTemplateId)
        {

            var itemTemplate = ItemManager.Instance.GetItemTemplateById(itemTemplateId);
            var classInfo = EntityClassManager.Instance.GetClassInfo(itemTemplate.Class);

            if (itemTemplate == null)
            {
                Logger.WriteLog(LogType.Error, $"RequestTooltipForItemTemplateId: Unknown itemTemplateId {itemTemplateId}");
                return; // todo: even answer on a unknown template, else the client will continue to spam us with requests
            }
            client.CallMethod(SysEntity.ClientGameUIManagerId, new ItemTemplateTooltipInfoPacket(itemTemplate, classInfo));
        }

        public void RequestTooltipForModuleId(Client client, int moduleId)
        {
            Logger.WriteLog(LogType.Debug, $"ToDo: RequestTooltipForModuleId");
            //var moduleInfo = new ItemModule(moduleId, 1, new ModuleInfo(1, 1, 1, 1, 1, 1, 1, 1, 1));

            //client.SendPacket(12, new ModuleTooltipInfoPacket(moduleInfo));
        }

        public bool ValidateItemEquip(Client client, Item itemToEquip)
        {
            var canEquip = true;
            // min level criteria met?
            if (itemToEquip != null)
            {
                // check requirements
                foreach (var requirement in itemToEquip.ItemTemplate.ItemInfo.Requirements)
                {
                    switch (requirement.Key)
                    {
                        case RequirementsType.ReqXpLevel:
                            if (client.Player.Level < itemToEquip.ItemTemplate.ItemInfo.Requirements[RequirementsType.ReqXpLevel])
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Level too low, cannot equip item.");
                                canEquip = false;
                            }

                            break;
                        case RequirementsType.ReqBody:
                            if (client.Player.Attributes[Attributes.Body].Current < itemToEquip.ItemTemplate.ItemInfo.Requirements[RequirementsType.ReqBody])
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Body attribute too low, cannot equip item.");
                                canEquip = false;
                            }

                            break;
                        case RequirementsType.ReqMind:
                            if (client.Player.Attributes[Attributes.Mind].Current < itemToEquip.ItemTemplate.ItemInfo.Requirements[RequirementsType.ReqMind])
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Mind attribute too low, cannot equip item.");
                                canEquip = false;
                            }

                            break;
                        case RequirementsType.ReqSpirit:
                            if (client.Player.Attributes[Attributes.Spirit].Current < itemToEquip.ItemTemplate.ItemInfo.Requirements[RequirementsType.ReqSpirit])
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Spirit attribute too low, cannot equip item.");
                                canEquip = false;
                            }

                            break;

                        case RequirementsType.ReqXpLevelMax:
                            if (client.Player.Level > itemToEquip.ItemTemplate.ItemInfo.Requirements[RequirementsType.ReqXpLevelMax])
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Level too high, cannot equip item.");
                                canEquip = false;
                            }

                            break;

                        default:
                            Logger.WriteLog(LogType.Error, $"Unknown RequirementsType {requirement.Key}");
                            break;
                    }
                }

                // check race requirements
                if (itemToEquip.ItemTemplate.ItemInfo.RaceReq != 0 && itemToEquip.ItemTemplate.ItemInfo.RaceReq != client.Player.Race)
                {
                    CommunicatorManager.Instance.SystemMessage(client, "Item is not for your race, cannot equip it.");
                    canEquip = false;
                }

                // check skill requrements if it's still true
                if (canEquip)
                    if (itemToEquip.ItemTemplate.EquipableInfo != null)
                    {
                        if (client.Player.Skills.ContainsKey((SkillId)itemToEquip.ItemTemplate.EquipableInfo.SkillId))
                        {
                            if (client.Player.Skills[(SkillId)itemToEquip.ItemTemplate.EquipableInfo.SkillId].SkillLevel >= itemToEquip.ItemTemplate.EquipableInfo.SkillLevel)
                                canEquip = true;
                            else
                            {
                                CommunicatorManager.Instance.SystemMessage(client, "Skill level to low, cannot equip item.");
                                canEquip = false;
                            }
                        }
                        else
                        {
                            CommunicatorManager.Instance.SystemMessage(client, $"{(SkillId)itemToEquip.ItemTemplate.EquipableInfo.SkillId} not learned, cannot equip item.");
                            canEquip = false;
                        }
                    }
            }

            return canEquip;
        }

        public void RefreshClanLockbox(uint clanId, ulong entityId, uint characterId, uint slotId, ref List<ulong> clanInventory, bool addBySlot)
        {
            if (addBySlot)
                ClanManager.Instance.CallMethodForOnlineMembers(clanId, (client) => AddItemBySlot(client, InventoryType.ClanInventory, entityId, slotId, false), characterId);

            ClanManager.Instance.CallMethodForOnlineMembers(clanId, (client) => UpdateItemSlot(client, entityId), characterId);
            ClanManager.Instance.CallMethodForOnlineMembers(clanId, (uint)SysEntity.ClientInventoryManagerId, new ClanInventoryReload(InventoryType.ClanInventory, clanInventory, 500));
        }

        public void RemoveItemBySlotForClan(uint clanId, uint slotId, uint skipThisCharacter)
        {
            ClanManager.Instance.CallMethodForOnlineMembers(clanId, (client) => RemoveItemBySlot(client, InventoryType.ClanInventory, slotId), skipThisCharacter);
        }

        #endregion
    }
}
