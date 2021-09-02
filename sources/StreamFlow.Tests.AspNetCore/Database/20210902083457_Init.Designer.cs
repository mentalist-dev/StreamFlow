﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using StreamFlow.Tests.AspNetCore.Database;

namespace StreamFlow.Tests.AspNetCore.Database
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20210902083457_Init")]
    partial class Init
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("Relational:MaxIdentifierLength", 63)
                .HasAnnotation("ProductVersion", "5.0.9")
                .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            modelBuilder.Entity("StreamFlow.Outbox.Entities.OutboxLock", b =>
                {
                    b.Property<string>("OutboxLockId")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.HasKey("OutboxLockId");

                    b.ToTable("Locks", "StreamFlow");
                });

            modelBuilder.Entity("StreamFlow.Outbox.Entities.OutboxMessage", b =>
                {
                    b.Property<Guid>("OutboxMessageId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<byte[]>("Body")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<DateTime>("Created")
                        .HasColumnType("timestamp without time zone");

                    b.Property<byte[]>("Options")
                        .HasColumnType("bytea");

                    b.Property<DateTime?>("Published")
                        .HasColumnType("timestamp without time zone");

                    b.Property<DateTime>("Scheduled")
                        .HasColumnType("timestamp without time zone");

                    b.Property<string>("TargetAddress")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)");

                    b.HasKey("OutboxMessageId");

                    b.HasIndex("Created", "Scheduled", "Published")
                        .HasDatabaseName("IX__Messages__Created__Scheduled__Published");

                    b.ToTable("Messages", "StreamFlow");
                });
#pragma warning restore 612, 618
        }
    }
}
