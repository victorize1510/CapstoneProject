using System;
using UnityEngine;

namespace GDS.Core.Events {

    public abstract class CustomEvent { }

    // Command
    public class Command : CustomEvent { }

    // TODO: Get rid of these?
    public class OpenUI : Command { }
    public class CloseUI : Command { }
    public class ToggleUI : Command { }
    public class RotateGhostItem : Command { }

    public class ItemCommand : Command {
        public Bag Bag;
        public Slot Slot;
        public Item Item;
        public EventModifiers Mods;
        public ItemCommand(Bag bag, Slot slot, Item item, EventModifiers mods) => (Bag, Slot, Item, Mods) = (bag, slot, item, mods);
    }

    public class PickItem : Command {
        public IItemContext Context;
        public Bag Bag => Context.Bag;
        public Slot Slot => Context.Slot;
        public Item Item => Context.Item;
        public PickItem(IItemContext context) => Context = context;
        public override string ToString() => $"Bag: {Bag}, Slot: {Slot}";
    }

    public class PlaceGhostItem : Command {
        public IItemContext Context;
        public Bag Bag => Context.Bag;
        public Slot Slot => Context.Slot;
        public PlaceGhostItem(IItemContext context) => Context = context;
        public override string ToString() => $"Bag: {Bag}, Slot: {Slot}";
    }

    public class DropGhostItem : Command {
        public bool IsOverUi = false;
        public GameObject GameObject;
        public Vector2 ScreenPosition;
        public Vector3 WorldPosition;
        public override string ToString() => $"{GetType().Name}, IsOverUi: {IsOverUi}, WorldPosition: {WorldPosition}";
    }

    public class SpawnWorldItem : Command {
        public Item Item;
        public Vector3 Pos;
        public SpawnWorldItem(Item item, Vector3 pos) => (Item, Pos) = (item, pos);
        public SpawnWorldItem(Item item) => Item = item;
        public override string ToString() => $"{GetType().Name}, Item: {Item}, Pos: {Pos}";
    }

    public class DespawnWorldItem : Command {
        public IWorldItem WorldItem;
        public DespawnWorldItem(IWorldItem worldItem) => WorldItem = worldItem;
        public override string ToString() => $"{GetType().Name}, WorldItem: {WorldItem}";
    }

    public class PickWorldItem : Command {
        public IWorldItem WorldItem;
        public PickWorldItem(IWorldItem worldItem) => WorldItem = worldItem;
        public override string ToString() => $"{GetType().Name}, WorldItem: {WorldItem}";
    }

    public class PickWorldItemSuccess : Success {
        public IWorldItem WorldItem;
        public PickWorldItemSuccess(IWorldItem worldItem) => WorldItem = worldItem;
        public override string ToString() => $"{GetType().Name}, WorldItem: {WorldItem}";
    }

    public class DropWorldItemSuccess : Success {
        public Item Item;
        public Vector3 Pos;
        public DropWorldItemSuccess(Item item, Vector3 pos) => (Item, Pos) = (item, pos);
        public DropWorldItemSuccess(Item item) => Item = item;
        public override string ToString() => $"{GetType().Name}, Item: {Item}, Pos: {Pos}";
    }

    // Window
    public class WindowCommand : Command {
        public object Handle;
        public WindowCommand(object handle) => Handle = handle;
    }
    public class OpenWindow : WindowCommand {
        public OpenWindow(object handle) : base(handle) { }
    }
    public class CloseWindow : WindowCommand {
        public CloseWindow(object handle) : base(handle) { }
    }



    // Result
    public class Result : CustomEvent {
        public static Result Success = new Success();
        public static Result Fail = new Fail();
    }

    // Fail
    public class Fail : Result {
        public static Fail NullRef = new NullRef();
        public static Fail RestrictedSlot = new RestrictedSlot();
        public static Fail WrongSlotType = new ItemNotAccepted();
        public static Fail ItemNotAccepted = new ItemNotAccepted();
        public static Fail ItemCannotFit = new ItemCannotFit();
        public static Fail StackingNotAllowed = new StackingNotAllowed();
    }
    public class NullRef : Fail { }
    public class RestrictedSlot : Fail { }
    public class WrongSlotType : Fail { }
    public class ItemNotAccepted : Fail { }
    public class SourceBagEmpty : Fail { }
    public class ItemCannotFit : Fail { }
    public class StackingNotAllowed : Fail { }

    // Success
    public class Success : Result { }
    public class ItemSuccess : Success {
        public Item Item;
        public ItemSuccess(Item item) => Item = item;
    }

    public class PickItemSuccess : ItemSuccess {
        public PickItemSuccess(Item item) : base(item) { }
    }

    public class PlaceItemSuccess : ItemSuccess {
        public Item Replaced;
        public PlaceItemSuccess(Item item, Item replaced) : base(item) => Replaced = replaced;
    }

    public class BuyItemSuccess : PickItemSuccess {
        public BuyItemSuccess(Item item) : base(item) { }
    }

    public class SellItemSuccess : PlaceItemSuccess {
        public SellItemSuccess(Item item) : base(item, null) { }
    }

    public class CraftItemSuccess : PickItemSuccess {
        public CraftItemSuccess(Item item) : base(item) { }
    }

    // Extensions
    public static class ResultExt {
        public static Result MapTo(this Result result, Success success, Fail fail = null) => result switch {
            Success => success,
            _ => fail ?? Result.Fail
        };

        public static Result MapTo(this Result result, Func<Result> action) => result is Success ? action() : result;
        public static Result And(this Result result, Func<Result> action) => result is Success ? action() : result;
    }
}