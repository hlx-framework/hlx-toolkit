package;

// Paired with Dog.hx: ConstructorCollector must not confuse Dog's own `new` with its internal super(name) call.
class Animal {
    public var name:String;

    public function new(name:String) {
        this.name = name;
    }

    public function speak():String {
        return name + " makes a sound";
    }
}
