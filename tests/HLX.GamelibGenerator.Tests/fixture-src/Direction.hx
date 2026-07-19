package;

// East/West are never constructed anywhere; survive only because of -dce no.
enum Direction {
    North;
    South;
    East;
    West;
}
