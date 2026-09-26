public partial class Regen : Affectables
{
	public override void _Ready()
	{
		BodyEntered += (_) => OnEnter();
		AreaEntered += (_) => OnEnter();
	}

	private void OnEnter()
	{
		QueueFree();
	}
}
