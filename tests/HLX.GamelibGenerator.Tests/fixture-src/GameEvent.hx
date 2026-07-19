package;

// Covers every ctor param shape (Int/String/Float/enum/Array); ItemsCollected is never constructed, kept by -dce no.
enum GameEvent {
    Started;
    ScoreChanged(delta:Int, reason:String);
    PositionUpdated(x:Float, y:Float);
    DirectionChosen(dir:Direction);
    ItemsCollected(items:Array<String>);
}
