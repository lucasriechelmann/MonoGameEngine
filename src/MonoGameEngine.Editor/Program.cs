using MonoGameEngine.Editor;

// Optional first argument: a project (.mgeproj), solution or project file to open instead of the last one.
using var game = new EditorGame(args.FirstOrDefault());
game.Run();
