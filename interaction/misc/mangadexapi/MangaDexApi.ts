import axios from "axios";
import { config } from "../../Config";
import {
  AuthorAttributesSchema,
  GetMangaIdParamsSchema,
  GetSearchMangaParamsSchema,
  LocalizedStringSchema,
  MangaListSchema,
  MangaResponseSchema,
  MangaSchema,
} from "./Schema";
import CacheManager from "../CacheManager";

// TODO: handle api limits (we arent hitting them rn so not my problem)

export interface MangaDexManga {
  id: string;
  title: string;
  description: string;
  author: string;
}

export interface SearchResults {
  data: MangaDexManga[];
  limit: number;
  offset: number;
  total: number;
}

// TODO: Put in config
const PREFERRED_LANGUAGES = ["en", "jp"];

export default class MangaDexApi {
  public static async search(
    query: Partial<GetSearchMangaParamsSchema>
  ): Promise<SearchResults> {
    query.includes = ["author"];

    const res = await axios.get(`${config.mangadexUrl}/manga`, {
      params: query,
      headers: {
        "User-Agent": config.userAgent,
      },
    });

    const responseData: MangaListSchema = res.data;

    let searchResults: SearchResults = {
      data: responseData.data.map((manga: MangaSchema): MangaDexManga => {
        return MangaDexApi.mapMangaSchema(manga);
      }),
      limit: responseData.limit,
      offset: responseData.offset,
      total: responseData.total,
    };

    return searchResults;
  }

  public static async getMangaById(id: string): Promise<MangaDexManga> {
    const cache = CacheManager.getCache("mangadex.getmangabyid");

    if (cache.has(id)) {
      return cache.get(id);
    }

    const query: Partial<GetMangaIdParamsSchema> = {
      includes: ["author"],
    };

    const apiResult = await axios.get(`${config.mangadexUrl}/manga/${id}`, {
      params: query,
      headers: {
        "User-Agent": config.userAgent,
      },
    });

    const responseData: MangaResponseSchema = apiResult.data;

    const result = this.mapMangaSchema(responseData.data);

    cache.set(id, result);

    return result;
  }

  private static mapMangaSchema(manga: MangaSchema): MangaDexManga {
    const authorData = manga.relationships.find((relationship) => {
      return (relationship.type = "author");
    });

    const authorAttributes = authorData.attributes as AuthorAttributesSchema;

    return {
      id: manga.id,
      title: MangaDexApi.getLocalizedString(manga.attributes.title),
      description: MangaDexApi.getLocalizedString(manga.attributes.description),
      author: authorAttributes.name,
    };
  }

  private static getLocalizedString(data: LocalizedStringSchema): string {
    for (const lang of PREFERRED_LANGUAGES) {
      if (data[lang]) return data[lang];
    }

    return Object.values(data)[0];
  }
}
