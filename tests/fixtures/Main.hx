// Exercises enum-typed field/static-return/param positions for the gamelib generator.
enum FixtureState {
    Idle;
    Loading(progress:Int, label:String);
}

class Main {
    public var state:FixtureState;

    public function new() {}

    static function main() {
        var m = new Main();
        m.state = FixtureState.Idle;
        m.setState(FixtureState.Loading(50, "loading"));
        trace("hlx fixture");
        trace(getState());
    }

    public function setState(s:FixtureState):Void {
        state = s;
    }

    public static function getState():FixtureState {
        return FixtureState.Loading(50, "loading");
    }
}
