package;

// @:generic monomorphization target: Box_Int/Box_String must collapse back into one Box<T> via GenericGrouping.
@:generic
class Box<T> {
    public var value:T;

    public function new(v:T) {
        value = v;
    }
}
