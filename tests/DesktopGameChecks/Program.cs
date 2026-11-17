using DinnerTray;
using System.Drawing.Imaging;

internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        foreach(int width in new[]{1280,1920,3440})
        {
            using var game=new SlingGame(new Rectangle(0,0,width,1000));
            using var pet=new DesktopSheep(new WoollySheep(),new Point(200,200)); pet.Game=game;
            pet.Place(game.SlingAnchor.X,game.SlingAnchor.Y+26);
            Require(game.TryLoad(pet),"Load sheep in sling");
            using var other=new DesktopSheep(new WoollySheep(),new Point((int)game.SlingAnchor.X,(int)game.SlingAnchor.Y+26));
            Require(!game.TryLoad(other),"Only one sheep occupies sling");
            game.Pull(pet,new(game.SlingAnchor.X-1000,game.SlingAnchor.Y+1000));
            Require(Math.Abs(Math.Sqrt(Math.Pow(pet.Body.X-game.SlingAnchor.X,2)+Math.Pow(pet.Body.Y-26-game.SlingAnchor.Y,2))-145)<.1,"Elastic pull limit");
            game.Release(pet);
            Require(game.Loaded==null && pet.GameFlight && pet.Body.VelocityX>0 && pet.Body.VelocityY<0,"Release launches opposite pull");
            // Exercise the actual sling calibration and production collision engine.
            bool reached=false;
            for(int pull=50;pull<=115 && !reached;pull+=5)
            {
                game.Reset();pet.Place(game.SlingAnchor.X,game.SlingAnchor.Y+26);game.TryLoad(pet);
                game.Pull(pet,new(game.SlingAnchor.X-pull,game.SlingAnchor.Y+pull*.65f));game.Release(pet);
                for(int step=0;step<1200 && !pet.Body.Grounded;step++)
                {
                    game.World.Step(1f/240);
                    pet.Body.Step(1f/240,new[]{new Perch(0,width,1000)},game.World.Area,0);
                    if(!pet.Body.Grounded)game.World.Hit(pet.Body);
                }
                reached=game.World.Broken>0;
            }
            Require(reached,$"Pile reachable on {width}px display");
            game.Reset();pet.Place(game.SlingAnchor.X,game.SlingAnchor.Y+26);game.TryLoad(pet);game.Reset();
            Require(game.Loaded==null,"Reset releases loaded sheep");
            Console.WriteLine($"PASS: loading, elastic limit, launch, reachable pile, reset at {width}px");
        }
        using var preview=new SlingGame(new Rectangle(0,0,1280,420));
        using var woolly=new DesktopSheep(new WoollySheep(),new Point(0,0));woolly.Game=preview;
        woolly.Place(preview.SlingAnchor.X,preview.SlingAnchor.Y+26);preview.TryLoad(woolly);
        preview.Pull(woolly,new(preview.SlingAnchor.X-80,preview.SlingAnchor.Y+52));
        preview.BackColor=Color.FromArgb(29,34,43);
        using var bitmap=new Bitmap(1280,420);preview.DrawToBitmap(bitmap,new Rectangle(0,0,1280,420));
        using(var g=Graphics.FromImage(bitmap)) {g.TranslateTransform(woolly.Left,woolly.Top);new WoollySheep().Draw(g,CreaturePose.Held,1,1);}
        Directory.CreateDirectory("test-results");bitmap.Save("test-results/slingshot-preview.png",ImageFormat.Png);
        Console.WriteLine("PASS: rendered game preview");
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
}
