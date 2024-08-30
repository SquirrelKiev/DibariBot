﻿// <auto-generated />
using System;
using DibariBot.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DibariBot.Migrations.SqliteMigrations
{
    [DbContext(typeof(SqliteContext))]
    partial class SqliteContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.8");

            modelBuilder.Entity("DibariBot.Database.GuildConfig", b =>
                {
                    b.Property<ulong>("GuildId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int?>("EmbedColor")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Prefix")
                        .IsRequired()
                        .HasMaxLength(8)
                        .HasColumnType("TEXT");

                    b.HasKey("GuildId");

                    b.ToTable("GuildConfigs");
                });

            modelBuilder.Entity("DibariBot.Database.Models.DefaultManga", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ChannelId")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Manga")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("GuildId", "ChannelId")
                        .IsUnique();

                    b.ToTable("DefaultMangas");
                });

            modelBuilder.Entity("DibariBot.Database.Models.RegexChannelEntry", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("ChannelId")
                        .HasColumnType("INTEGER");

                    b.Property<uint>("RegexFilterId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("RegexFilterId");

                    b.HasIndex("ChannelId", "RegexFilterId");

                    b.ToTable("RegexChannelEntries");
                });

            modelBuilder.Entity("DibariBot.Database.Models.RegexFilter", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("ChannelFilterScope")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Filter")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("FilterType")
                        .HasColumnType("INTEGER");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Template")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("GuildId");

                    b.ToTable("RegexFilters");
                });

            modelBuilder.Entity("DibariBot.Database.Models.RegexChannelEntry", b =>
                {
                    b.HasOne("DibariBot.Database.Models.RegexFilter", "RegexFilter")
                        .WithMany("RegexChannelEntries")
                        .HasForeignKey("RegexFilterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("RegexFilter");
                });

            modelBuilder.Entity("DibariBot.Database.Models.RegexFilter", b =>
                {
                    b.Navigation("RegexChannelEntries");
                });
#pragma warning restore 612, 618
        }
    }
}
