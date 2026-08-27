using Xunit;

// Plusieurs classes de tests redirigent Console.Out/Console.Error (etat global partage,
// non thread-safe) pour capturer la sortie des handlers de commandes. La parallelisation
// par defaut de xUnit (classes de test differentes = threads differents) provoque des
// courses entre ces redirections et fait "fuiter" la sortie d'un test dans un autre
// (constate en CI sur ubuntu-latest, invisible localement selon le timing). Desactivee
// pour tout l'assemblage plutot que de synchroniser chaque test individuellement.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
