﻿using Microsoft.EntityFrameworkCore;

namespace Autumn.Data.Mvc.EntityFramework.Samples.Models
{
    public class ChinookContext : DbContext
    {
        public DbSet<Artist> Artists { get; set; }
        
        public DbSet<Genre> Genres { get; set; }

        public ChinookContext(DbContextOptions options) : base(options)
        {
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Artist>()
                .ToTable("Artist")
                .Property(a=>a.Id).HasColumnName("ArtistId");
            
            
            modelBuilder.Entity<Genre>()
                .ToTable("Genre")
                .Property(o=>o.Id).HasColumnName("GenreId");

            modelBuilder.Entity<Album>()
                .ToTable("Album")
                .Property(a => a.Id).HasColumnName("AlbumId");
            
            modelBuilder.Entity<Customer>()
                .ToTable("Customer")
                .Property(a => a.Id).HasColumnName("CustomerId");
        }
    }
}