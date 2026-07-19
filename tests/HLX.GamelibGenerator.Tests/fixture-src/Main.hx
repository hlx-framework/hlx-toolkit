package;

import fixture.pkg.Sub;

// References every fixture type so the compiler types it; compiled with -dce no, and never actually run.
class Main {
    static function main() {
        var w = new Widget(1, "hello");
        w.onClick = function() {};
        w.bind(function() {
            return function() {};
        });
        var boxed = 0;
        w.addInto(hl.Ref.make(boxed), 5);
        trace(w.describe());
        trace(w.maybe(5));
        trace(w.maybe());
        Widget.bump();
        Widget.resolution = 3;
        Widget.ratio = 2.0;

        var a = new Animal("Rex");
        var d = new Dog("Fido", "Lab");
        trace(a.speak());
        trace(d.speak());

        var nc = new NoExplicitCtor("Rex");
        trace(nc.tag);

        // Typed but never `new`'d - see NeverInstantiated.hx.
        var never:NeverInstantiated = null;
        trace(never);

        var boxInt = new Box<Int>(42);
        var boxStr = new Box<String>("hi");
        trace(boxInt.value);
        trace(boxStr.value);

        var bus = new EventBus();
        bus.fire(GameEvent.ScoreChanged(10, "combo"));
        bus.fire(GameEvent.PositionUpdated(1.0, 2.0));
        bus.fire(GameEvent.DirectionChosen(Direction.North));
        trace(EventBus.currentDirection());
        trace(Direction.South);

        var sub = new Sub("hi");
        Sub.increment();
        Sub.ratio = 1.5;
        sub.value = 7;
        trace(sub.label);
        trace(Sub.counter);
    }
}
