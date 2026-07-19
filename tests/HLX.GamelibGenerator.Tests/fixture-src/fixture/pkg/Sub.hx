package fixture.pkg;

// Packaged class: real companion is "fixture.pkg.$Sub", not "$fixture.pkg.Sub" (a real bug this generator had).
class Sub {
    public static var counter:Int = 0;

    @:isVar public static var ratio(get, set):Float;
    static function get_ratio():Float {
        return ratio;
    }
    static function set_ratio(v:Float):Float {
        return ratio = v;
    }

    public static function increment():Int {
        counter++;
        return counter;
    }

    public var label:String;

    public var value(default, set):Int;
    function set_value(v:Int):Int {
        value = v;
        return v;
    }

    public function new(label:String) {
        this.label = label;
        value = 0;
    }
}
