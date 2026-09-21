using System;
using System.Collections.Generic;

namespace dotnet.core.thegoldenfan.Dbs;

// Une reponse du defi culture club. Une ligne par joueur et par question :
// elle nait au moment ou la question s'affiche (StartedAt), et se ferme quand
// le joueur repond (AnsweredAt). Le chrono est donc tenu par le serveur, et
// personne ne peut s'offrir quinze secondes de plus en trafiquant son telephone.
public partial class QuizAnswer
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    // "q20260923-1" : la date du jour de la question, puis son rang (1, 2 ou 3).
    public string QuestionId { get; set; } = null!;

    public DateTime StartedAt { get; set; }

    public DateTime? AnsweredAt { get; set; }

    // Le rang de la reponse choisie, de 0 a 3. -1 tant que rien n'est choisi.
    public int Choice { get; set; }

    public bool Correct { get; set; }

    public int Points { get; set; }

    // Le temps de reflexion, en millisecondes. Il departage les ex aequo.
    public int TimeMs { get; set; }
}
