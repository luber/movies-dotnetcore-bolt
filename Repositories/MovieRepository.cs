using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MoviesDotNetCore.Model;
using Neo4jClient;
using Neo4jClient.Cypher;

namespace MoviesDotNetCore.Repositories;

public interface IMovieRepository
{
    Task<Movie> FindByTitle(string title);
    Task<int> VoteByTitle(string title);
    Task<List<Movie>> Search(string search);
    Task<D3Graph> FetchD3Graph(int limit);
}

public class MovieRepository : IMovieRepository
{
    private readonly IGraphClient _client;

    public MovieRepository(IGraphClient client)
    {
        _client = client;
    }
    
    public async Task<Movie> FindByTitle(string title)
    {
        var query = _client.Cypher
            .Match("(movie:Movie {title:$title})")
            .OptionalMatch("(movie)<-[r]-(person:Person)")
            .WithParam("title", title)
            .Return((movie, person, r) => new
            {
                Title = Return.As<string>("movie.title"),
                Cast = Return.As<List<Dictionary<string, object>>>("collect({name:person.name, job: head(split(toLower(type(r)),'_')), role: r.roles})")
            });

        var result = await query.ResultsAsync;
        var movieData = result.Single();

        return new Movie(
            movieData.Title,
            MapCast(movieData.Cast));
    }

    public async Task<int> VoteByTitle(string title)
    {
        var query = _client.Cypher
            .Match("(m:Movie {title: $title})")
            .WithParam("title", title)
            .Set("m.votes = coalesce(m.votes, 0) + 1")
            .Return(m => Return.As<int>("count(m)"));

        var result = await query.ResultsAsync;
        return result.Single();
    }

    public async Task<List<Movie>> Search(string search)
    {
        var query = _client.Cypher
            .Match("(movie:Movie)")
            .Where("toLower(movie.title) CONTAINS toLower($title)")
            .WithParam("title", search)
            .Return((movie) => new
            {
                Title = Return.As<string>("movie.title"),
                Released = Return.As<long>("movie.released"),
                Tagline = Return.As<string>("movie.tagline"),
                Votes = Return.As<long?>("movie.votes")
            });

        var results = await query.ResultsAsync;

        return results.Select(r => new Movie(
            r.Title,
            Tagline: r.Tagline,
            Released: r.Released,
            Votes: r.Votes
        )).ToList();
    }

    public async Task<D3Graph> FetchD3Graph(int limit)
    {
        var query = _client.Cypher
            .Match("(m:Movie)<-[:ACTED_IN]-(p:Person)")
            .With("m, p")
            .OrderBy("m.title, p.name")
            .Return((m, p) => new 
            { 
                Title = Return.As<string>("m.title"), 
                Cast = Return.As<List<string>>("collect(p.name)") 
            })
            .Limit(limit);

        var results = await query.ResultsAsync;
        
        var nodes = new List<D3Node>();
        var links = new List<D3Link>();

        foreach (var record in results)
        {
            var movie = new D3Node(record.Title, "movie");
            var movieIndex = nodes.Count;
            nodes.Add(movie);
            
            foreach (var actorName in record.Cast)
            {
                var actor = new D3Node(actorName, "actor");
                var actorIndex = nodes.IndexOf(actor);
                actorIndex = actorIndex == -1 ? nodes.Count : actorIndex;
                nodes.Add(actor);
                links.Add(new D3Link(actorIndex, movieIndex));
            }
        }

        return new D3Graph(nodes, links);
    }

    private static IEnumerable<Person> MapCast(IEnumerable<dynamic> persons)
    {
        return persons
            .Select(p => new Person(
                p["name"].ToString(),
                p["job"].ToString(),
                string.Join(", ", p["role"] as IEnumerable<string> ?? Array.Empty<string>())
            ))
            .ToList();
    }
}
