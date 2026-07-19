package;

// Referenced only as a type, never `new`'d - exercises ConstructorCollector's zero-candidate-sites path.
class NeverInstantiated {
    public var tag:String;

    public function new(tag:String) {
        this.tag = tag;
    }
}
