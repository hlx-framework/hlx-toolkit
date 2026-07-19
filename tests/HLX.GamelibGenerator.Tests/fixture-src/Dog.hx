package;

// Own ctor calls super(...); the paired Call must resolve to Dog's own findex, never Animal's.
class Dog extends Animal {
    public var breed:String;

    public function new(name:String, breed:String) {
        super(name);
        this.breed = breed;
    }

    override public function speak():String {
        return name + " barks (" + breed + ")";
    }
}
