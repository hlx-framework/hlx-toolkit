package;

// Root-package class covering plain/accessor fields, callbacks, ref/optional params, and std container types.
class Widget {
    public var plainField:Int;

    public var x(default, set):Float;
    function set_x(v:Float):Float {
        x = v;
        return v;
    }

    // @:isVar keeps a physical "y" field so get_y/set_y route through it (this is h2d.Object's set_x/set_y shape).
    @:isVar public var y(get, set):Float;
    function get_y():Float {
        return y;
    }
    function set_y(v:Float):Float {
        return y = v;
    }

    public var onClick:Void->Void;

    public static var plainStatic:Int = 0;

    public static var resolution(default, set):Int;
    static function set_resolution(v:Int):Int {
        resolution = v;
        return v;
    }

    @:isVar public static var ratio(get, set):Float;
    static function get_ratio():Float {
        return ratio;
    }
    static function set_ratio(v:Float):Float {
        return ratio = v;
    }

    public static function bump():Int {
        plainStatic++;
        return plainStatic;
    }

    public var tags:Array<Int>;
    public var lookup:Map<String, Int>;

    public function new(a:Int, b:String) {
        plainField = a;
        x = 0;
        y = 0;
        onClick = null;
        tags = [];
        lookup = new Map();
        trace(b);
    }

    public function maybe(?n:Int):Int {
        return n == null ? -1 : n;
    }

    public function addInto(r:hl.Ref<Int>, amount:Int):Void {
        r.set(r.get() + amount);
    }

    // Nested return-function-type: HaxeTypeMapper.MapFunctionType's extra-parens case ("() -> (() -> Void)").
    public function bind(handler:Void->(Void->Void)):Void {
        onClick = handler();
    }

    public function describe():String {
        return plainField + ":" + tags.length;
    }
}
