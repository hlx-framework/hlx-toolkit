package;

// No `function new()`, but the field initializer below makes Haxe synthesize its own ctor - distinct findex from Animal's.
class NoExplicitCtor extends Animal {
    public var tag:String = "sub";
}
