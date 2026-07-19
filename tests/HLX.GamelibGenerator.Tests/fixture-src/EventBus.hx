package;

// Exercises the three enum usage shapes (field/param/static-return) against real compiled bytecode.
class EventBus {
    public var lastEvent:GameEvent;

    public function new() {
        lastEvent = GameEvent.Started;
    }

    public function fire(e:GameEvent):Void {
        lastEvent = e;
    }

    public static function currentDirection():Direction {
        return Direction.North;
    }
}
